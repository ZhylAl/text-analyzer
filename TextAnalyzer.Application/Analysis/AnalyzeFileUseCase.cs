using Microsoft.Extensions.Logging;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Mappers;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Application.Services;
using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFileUseCase
{
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<AnalyzeFileUseCase> _logger; 
    private readonly IHashService _hashService;

    public AnalyzeFileUseCase(IFileReader fileReader, ITextAnalyzerService analyzerService, ISessionRepository sessionRepository, ILogger<AnalyzeFileUseCase> logger, IHashService hashService)
    {
        _fileReader = fileReader;
        _analyzerService = analyzerService;
        _sessionRepository = sessionRepository;
        _logger = logger;
        _hashService = hashService; 
    }

    public async Task<TextAnalysisResult> Execute(string filePath, CancellationToken ct)
    {
        _logger.LogInformation("Starting analysis for file: {FilePath}", filePath);

        var startedAt = DateTime.UtcNow;
        string text = await _fileReader.ReadAllTextAsync(filePath, ct);

        string fileHash = _hashService.ComputeSha256Hash(text);

        TextAnalysisResult result;

        var cachedResult = await _sessionRepository.GetCachedResultAsync(fileHash);
        if (cachedResult != null)
        {
            _logger.LogInformation("Found cached result for file: {FilePath}", filePath);
            result = cachedResult;
        }
        else
        {
             result = _analyzerService.Analyze(text);
        }

        var finishedAt = DateTime.UtcNow;
        var dto = new SessionSaveDto
        (
            startedAt,
            finishedAt,
            (int)ExecutionMode.SingleFile,
            new List<FileAnalysisResult>
            {
                new FileAnalysisResult(filePath, result, fileHash)
            }
        );

        await _sessionRepository.AddAsync(dto.ToEntity());

        _logger.LogInformation("Analysis finished for file: {FilePath} with Hash: {Hash}. Found {WordCount} words...", filePath, fileHash, result.WordCount);
        return result;
    }
}