using TextAnalyzer.Application.FileReader;

namespace TextAnalyzer.Infrastructure.FileReader;

public class LocalFileReader : IFileReader
{
    public string ReadAllText(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"The file at {filePath} was not found.");
        }
        
        return File.ReadAllText(filePath);
    }
}
