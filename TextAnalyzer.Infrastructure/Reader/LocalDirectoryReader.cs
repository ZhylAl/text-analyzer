using TextAnalyzer.Application.Interfaces;

namespace TextAnalyzer.Infrastructure.Reader;

public class LocalDirectoryReader : IDirectoryReader
{
    private readonly string[] _allowedExtensions;

    public LocalDirectoryReader(string[] allowedExtensions)
    {
        _allowedExtensions = allowedExtensions;
    }

    public IEnumerable<string> GetTextFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"The directory at {directoryPath} was not found.");
        }

        return Directory.GetFiles(directoryPath)
            .Where(file => _allowedExtensions.Contains(Path.GetExtension(file).ToLower()));
    }
}
