namespace TextAnalyzer.Application.Reader;

public interface IFileReader
{
    Task<string> ReadAllTextAsync(string filePath, CancellationToken ct);
}