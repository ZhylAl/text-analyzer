namespace TextAnalyzer.Application.Interfaces;

public interface IFileReader
{
    Task<string> ReadAllTextAsync(string filePath, CancellationToken ct);
}