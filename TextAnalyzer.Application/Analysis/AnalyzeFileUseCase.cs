using Microsoft.Extensions.Logging;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Mappers;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFileUseCase
{
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<AnalyzeFileUseCase> _logger; 

    public AnalyzeFileUseCase(IFileReader fileReader, ITextAnalyzerService analyzerService, ISessionRepository sessionRepository, ILogger<AnalyzeFileUseCase> logger)
    {
        _fileReader = fileReader;
        _analyzerService = analyzerService;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<TextAnalysisResult> Execute(string filePath, CancellationToken ct)
    {
        _logger.LogInformation("Starting analysis for file: {FilePath}", filePath);

        var startedAt = DateTime.UtcNow;
        string text = await _fileReader.ReadAllTextAsync(filePath, ct);
        var result = _analyzerService.Analyze(text);

        var finishedAt = DateTime.UtcNow;
        var dto = new SessionSaveDto
        (
            startedAt,
            finishedAt,
            (int)ExecutionMode.SingleFile,
            new List<FileAnalysisResult>
            {
                new FileAnalysisResult(filePath, result)
            }
        );

        await _sessionRepository.AddAsync(dto.ToEntity());

        _logger.LogInformation("Analysis finished for file: {FilePath}. Found {WordCount} words and {CharCount} characters", filePath, result.WordCount, result.CharCount);
        return result;
    }
}