using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Interfaces;

public interface ITextAnalyzerService
{
    TextAnalysisResult Analyze(string text);
}