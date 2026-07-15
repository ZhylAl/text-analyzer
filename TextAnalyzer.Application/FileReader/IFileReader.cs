namespace TextAnalyzer.Application.FileReader;

public interface IFileReader
{
    string ReadAllText(string filePath);
}