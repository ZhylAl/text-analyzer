using System.Collections.Concurrent;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Domain.Services;

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFolderUseCase
{
    private readonly IDirectoryReader _directoryReader;
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;
    private readonly IFileAnalysisResultWriter _resultWriter;
    private readonly ISessionRepository _sessionRepository;

    public AnalyzeFolderUseCase(
        IDirectoryReader directoryReader,
        IFileReader fileReader,
        ITextAnalyzerService analyzerService,
        IFileAnalysisResultWriter resultWriter,
        ISessionRepository sessionRepository)
    {
        _directoryReader = directoryReader;
        _fileReader = fileReader;
        _analyzerService = analyzerService;
        _resultWriter = resultWriter;
        _sessionRepository = sessionRepository;
    }

    public async Task<string> Execute(string folderPath, CancellationToken ct = default)
    {
        var startedAt = DateTime.UtcNow;
        var filePaths = _directoryReader.GetTextFiles(folderPath);
        var results = new ConcurrentBag<FileAnalysisResult>();

        // Used Parallel.ForEach because slicing strings and counting 
        // characters is mainly CPU work and underlying methods are synchronous
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(filePaths, parallelOptions, async (filePath, cancellationToken) =>
        {
            try
            {
                string text = await _fileReader.ReadAllTextAsync(filePath, cancellationToken);
                var analysisResult = _analyzerService.Analyze(text);
                
                results.Add(new FileAnalysisResult(filePath, analysisResult));
            }
            catch (UnauthorizedAccessException ex)
            {
                Console.WriteLine($"[Warning] Access denied to file: {filePath}. Skipping...");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"[Warning] Could not read file {filePath}. It might be in use. Details: {ex.Message}");
            }
            catch(Exception ex)
            {
                // Just skip files that cant be read
                // In a real application I would log this exception
                Console.WriteLine($"[Warning] Could not read file {filePath}. Details: {ex.Message}");
            }
        });

        // Map domain results to Export DTOs
        var exportDtos = results.Select(r => new FileAnalysisExportDto(
            Path.GetFileName(r.FilePath),
            r.FilePath,
            r.AnalysisResult.CharCount,
            r.AnalysisResult.WordCount,
            r.AnalysisResult.LineCount,
            r.AnalysisResult.LongestWord
        )).ToList();

        // Write results to CSV in the target folder
        string outputCsvPath = Path.Combine(folderPath, "results.csv");
        _resultWriter.WriteResults(outputCsvPath, exportDtos);

        // Find and return the longest word overall
        var longestWordOverall = exportDtos
            .MaxBy(r => r.LongestWord.Length)?
            .LongestWord ?? string.Empty;

        var finishedAt = DateTime.UtcNow;
        await _sessionRepository.AddAsync(new SessionSaveDto
        (
            startedAt,
            finishedAt,
            (int)ExecutionMode.Folder,
            results
        ));

        return longestWordOverall;
    }
}
