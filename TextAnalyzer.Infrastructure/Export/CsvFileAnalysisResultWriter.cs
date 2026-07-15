using System.Globalization;
using CsvHelper;
using TextAnalyzer.Application.Export;
using TextAnalyzer.Application.Models;

namespace TextAnalyzer.Infrastructure.Export;

public class CsvFileAnalysisResultWriter : IFileAnalysisResultWriter
{
    public void WriteResults(string outputFilePath, IEnumerable<FileAnalysisExportDto> results)
    {
        using var writer = new StreamWriter(outputFilePath);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        
        csv.WriteRecords(results);
    }
}
