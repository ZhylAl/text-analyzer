using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;  

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFolderUseCase
{
    private readonly IDirectoryReader _directoryReader;
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;
    private readonly IFileAnalysisResultWriter _resultWriter;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<AnalyzeFolderUseCase> _logger;
    private readonly IHashService _hashService;

    public AnalyzeFolderUseCase(
        IDirectoryReader directoryReader,
        IFileReader fileReader,
        ITextAnalyzerService analyzerService,
        IFileAnalysisResultWriter resultWriter,
        IApplicationDbContext dbContext,
        ILogger<AnalyzeFolderUseCase> logger,
        IHashService hashService)
    {
        _directoryReader = directoryReader;
        _fileReader = fileReader;
        _analyzerService = analyzerService;
        _resultWriter = resultWriter;
        _dbContext = dbContext;
        _logger = logger;
        _hashService = hashService;
    }

    public async Task<AnalyzeFolderResponse> Execute(string folderPath, CancellationToken ct = default)
    {
        _logger.LogInformation("Started analysis for folder: {FolderPath}", folderPath);

        var startedAt = DateTime.UtcNow;
        var filePaths = _directoryReader.GetTextFiles(folderPath);
        var results = new ConcurrentBag<FileAnalysisResult>();
        var errors = new ConcurrentBag<string>();
        var fileData = new ConcurrentBag<(string Path, string Hash, string Text)>();
        
        // Used Parallel.ForEach because slicing strings and counting 
        // characters is mainly CPU work and underlying methods are synchronous
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        };

        await Parallel.ForEachAsync(filePaths, parallelOptions, async (filePath, token) => {
            try
            {
                string text = await _fileReader.ReadAllTextAsync(filePath, token);
                string hash = _hashService.ComputeSha256Hash(text);
                fileData.Add((filePath, hash, text));
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while reading file: {FilePath}", filePath);
                errors.Add($"[Warning] Could not read file {filePath}. Details: {ex.Message}");
            }
        });

        var allHashes = fileData.Select(x => x.Hash).ToList();
        var cachedFilesList = await _dbContext.Files
            .Include(f => f.Result)
            .Where(f => allHashes.Contains(f.FileHash))
            .ToListAsync(ct);

        // Group by hash and take the first item to safely build a dictionary.
        // This acts as a defensive mechanism against legacy duplicate records 
        // in the database that share the same FileHash, preventing ArgumentException 
        // (duplicate keys) during ToDictionary execution.
        var cachedFiles = cachedFilesList
            .GroupBy(f => f.FileHash)
            .ToDictionary(g => g.Key, g => g.First());

        await Parallel.ForEachAsync(fileData, parallelOptions, async (data, cancellationToken) =>
        {
            try
            {
                TextAnalysisResult result;

                if (cachedFiles.TryGetValue(data.Hash, out var cachedEntity))
                {
                    _logger.LogInformation("Found cached result for file: {FilePath}", data.Path);
                    result = new TextAnalysisResult(
                        cachedEntity.Result.CharCount,
                        cachedEntity.Result.WordCount,
                        cachedEntity.Result.LineCount,
                        cachedEntity.Result.LongestWord
                    );
                }
                else
                {
                    result = _analyzerService.Analyze(data.Text);
                }

                results.Add(new FileAnalysisResult(data.Path, result, data.Hash));
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while analyzing file: {FilePath}", data.Path);
                errors.Add($"[Warning] Could not analyze file {data.Path}. Details: {ex.Message}");
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

        var session = new SessionEntity
        {
            Id = Guid.NewGuid(),
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
            ExecutionModeId = (int)ExecutionMode.Folder,
            Files = new List<FileEntity>()
        };

        foreach (var data in fileData)
        {
            if (cachedFiles.TryGetValue(data.Hash, out var existingFile))
            {
                session.Files.Add(existingFile);
            }
            else
            {
                var analysisResult = results.First(r => r.Hash == data.Hash).AnalysisResult;

                var newFile = new FileEntity
                {
                    Id = Guid.NewGuid(),
                    FilePath = data.Path,
                    FileHash = data.Hash,
                    Result = new ResultEntity
                    {
                        Id = Guid.NewGuid(),
                        CharCount = analysisResult.CharCount,
                        WordCount = analysisResult.WordCount,
                        LineCount = analysisResult.LineCount,
                        LongestWord = analysisResult.LongestWord
                    }
                };
                session.Files.Add(newFile);
            }
        }

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Analysis finished for folder {FolderPath}. Total files processed: {FileCount}. Total errors: {ErrorCount}", folderPath, results.Count, errors.Count);
        return new AnalyzeFolderResponse(longestWordOverall, errors.ToArray());
    }
}
