using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Mappers;
using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFolderUseCase
{
    private readonly IDirectoryReader _directoryReader;
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;
    private readonly IFileAnalysisResultWriter _resultWriter;
    private readonly ISessionRepository _sessionRepository;
    private readonly ILogger<AnalyzeFolderUseCase> _logger;

    public AnalyzeFolderUseCase(
        IDirectoryReader directoryReader,
        IFileReader fileReader,
        ITextAnalyzerService analyzerService,
        IFileAnalysisResultWriter resultWriter,
        ISessionRepository sessionRepository,
        ILogger<AnalyzeFolderUseCase> logger)
    {
        _directoryReader = directoryReader;
        _fileReader = fileReader;
        _analyzerService = analyzerService;
        _resultWriter = resultWriter;
        _sessionRepository = sessionRepository;
        _logger = logger;
    }

    public async Task<AnalyzeFolderResponse> Execute(string folderPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Started analysis for folder: {FolderPath}", folderPath);

        var startedAt = DateTime.UtcNow;
        var filePaths = _directoryReader.GetTextFiles(folderPath);
        var results = new ConcurrentBag<FileAnalysisResult>();
        var errors = new ConcurrentBag<string>();

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
                _logger.LogError(ex, "Access denied while reading file: {FilePath}", filePath);
                errors.Add($"Access denied to file: {filePath}. Skipping...");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Could not read file: {FilePath}", filePath);
                errors.Add($"[Warning] Could not read file {filePath}. It might be in use. Details: {ex.Message}");
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while reading file: {FilePath}", filePath);
                errors.Add($"[Warning] Could not read file {filePath}. Details: {ex.Message}");
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
        var dto = new SessionSaveDto
        (
            startedAt,
            finishedAt,
            (int)ExecutionMode.Folder,
            results
        );

        await _sessionRepository.AddAsync(dto.ToEntity());

        _logger.LogInformation("Analysis finished for folder {FolderPath}. Total files processed: {FileCount}. Total errors: {ErrorCount}", folderPath, results.Count, errors.Count);
        return new AnalyzeFolderResponse(longestWordOverall, errors.ToArray());
    }
}
