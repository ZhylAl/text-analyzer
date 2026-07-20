using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Application.Reader;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Domain.Services;

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFileUseCase
{
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;
    private readonly ISessionRepository _sessionRepository;

    public AnalyzeFileUseCase(IFileReader fileReader, ITextAnalyzerService analyzerService, ISessionRepository sessionRepository)
    {
        _fileReader = fileReader;
        _analyzerService = analyzerService;
        _sessionRepository = sessionRepository;
    }

    public async Task<TextAnalysisResult> Execute(string filePath, CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        string text = await _fileReader.ReadAllTextAsync(filePath, ct);
        var result = _analyzerService.Analyze(text);

        var finishedAt = DateTime.UtcNow;
        await _sessionRepository.AddAsync(new SessionSaveDto
        (
            startedAt,
            finishedAt,
            (int)ExecutionMode.SingleFile,
            new List<FileAnalysisResult>
            {
                new FileAnalysisResult(filePath, result)
            }
        ));

        return result;
    }
}