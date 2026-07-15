using TextAnalyzer.Application.Models;

namespace TextAnalyzer.Application.Export;

public interface IFileAnalysisResultWriter
{
    void WriteResults(string outputFilePath, IEnumerable<FileAnalysisExportDto> results);
}
