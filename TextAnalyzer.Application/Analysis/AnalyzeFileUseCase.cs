using TextAnalyzer.Application.FileReader;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Domain.Services;

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFileUseCase
{
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;

    public AnalyzeFileUseCase(IFileReader fileReader, ITextAnalyzerService analyzerService)
    {
        _fileReader = fileReader;
        _analyzerService = analyzerService;
    }

    public TextAnalysisResult Execute(string filePath)
    {
        string text = _fileReader.ReadAllText(filePath);

        return _analyzerService.Analyze(text);
    }
}