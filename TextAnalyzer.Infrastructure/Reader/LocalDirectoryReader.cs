using TextAnalyzer.Application.Reader;

namespace TextAnalyzer.Infrastructure.Reader;

public class LocalDirectoryReader : IDirectoryReader
{
    public IEnumerable<string> GetTextFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"The directory at {directoryPath} was not found.");
        }

        var allowedExtensions = new[] { ".txt", ".csv" };
        
        return Directory.GetFiles(directoryPath)
            .Where(file => allowedExtensions.Contains(Path.GetExtension(file).ToLower()));
    }
}
