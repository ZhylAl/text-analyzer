namespace TextAnalyzer.Domain.Models;

public record FileAnalysisResult(
    string FilePath,
    TextAnalysisResult AnalysisResult
);
