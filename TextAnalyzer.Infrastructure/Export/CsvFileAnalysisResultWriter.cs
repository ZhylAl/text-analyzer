using System.Globalization;
using CsvHelper;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;

namespace TextAnalyzer.Infrastructure.Export;

public class CsvFileAnalysisResultWriter : IFileAnalysisResultWriter
{
    public void WriteResults(string outputFilePath, IEnumerable<FileAnalysisExportDto> results)
    {
        using StreamWriter writer = new StreamWriter(outputFilePath);
        using CsvWriter csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
        
        csv.WriteRecords(results);
    }
}
