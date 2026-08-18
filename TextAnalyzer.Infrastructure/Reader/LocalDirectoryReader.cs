using Microsoft.Extensions.Options;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Infrastructure.Settings;

namespace TextAnalyzer.Infrastructure.Reader;

public class LocalDirectoryReader : IDirectoryReader
{
    private readonly string[] _allowedExtensions;

    public LocalDirectoryReader(IOptions<ReaderSettings> options)
    {
        _allowedExtensions = options.Value.AllowedExtensions;
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
