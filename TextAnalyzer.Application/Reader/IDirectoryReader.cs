namespace TextAnalyzer.Application.Reader;

public interface IDirectoryReader
{
    IEnumerable<string> GetTextFiles(string directoryPath);
}
