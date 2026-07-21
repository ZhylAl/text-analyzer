using TextAnalyzer.Application.Models;

namespace TextAnalyzer.Application.Interfaces;

public interface IFileAnalysisResultWriter
{
    void WriteResults(string outputFilePath, IEnumerable<FileAnalysisExportDto> results);
}
