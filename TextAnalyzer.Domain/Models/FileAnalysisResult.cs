namespace TextAnalyzer.Domain.Models;

public record FileAnalysisResult(
    string FileName,
    TextAnalysisResult AnalysisResult
);
