namespace TextAnalyzer.Application.Reader;

public interface IFileReader
{
    string ReadAllText(string filePath);
}