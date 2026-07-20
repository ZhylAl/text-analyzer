namespace TextAnalyzer.Application.Models;

public record FileAnalysisExportDto(
    string FileName,
    string FilePath,
    int CharCount,
    int WordCount,
    int LineCount,
    string LongestWord
);
