namespace TextAnalyzer.Application.Models;

public record AnalyzeFolderResponse(
    string LongestWordOverall,
    IReadOnlyCollection<string> Errors
);