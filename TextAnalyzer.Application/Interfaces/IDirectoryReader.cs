namespace TextAnalyzer.Application.Interfaces;

public interface IDirectoryReader
{
    IEnumerable<string> GetTextFiles(string directoryPath);
}
