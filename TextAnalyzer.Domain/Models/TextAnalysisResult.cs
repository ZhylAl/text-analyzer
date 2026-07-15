namespace TextAnalyzer.Domain.Models;

public record TextAnalysisResult(
    int CharCount,
    int WordCount,
    int LineCount,
    string LongestWord
);