using TextAnalyzer.Application.Reader;

namespace TextAnalyzer.Infrastructure.Reader;

public class LocalFileReader : IFileReader
{
    public async Task<string> ReadAllTextAsync(string filePath, CancellationToken ct)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file at {filePath} was not found.");
        }
        
        return await File.ReadAllTextAsync(filePath, ct);
    }
}
