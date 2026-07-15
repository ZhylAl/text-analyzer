namespace TextAnalyzer.Application.Models;

public record FileAnalysisExportDto(
    string FileName,
    int CharCount,
    int WordCount,
    int LineCount,
    string LongestWord
);
