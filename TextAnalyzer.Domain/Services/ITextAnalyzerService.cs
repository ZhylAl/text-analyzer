using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Domain.Services;

public interface ITextAnalyzerService
{
    TextAnalysisResult Analyze(string text);
}