using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Analysis
{
    public class ProcessBatchUseCase
    {
        private readonly IApplicationDbContext _dbContext;
        private readonly ITextAnalyzerService _analyzerService;
        private readonly IFileReader _fileReader;
        private readonly IHashService _hashService;
        private readonly ILogger<ProcessBatchUseCase> _logger;

        public ProcessBatchUseCase(
            IApplicationDbContext dbContext,
            ITextAnalyzerService analyzerService,
            IFileReader fileReader,
            IHashService hashService,
            ILogger<ProcessBatchUseCase> logger)
        {
            _dbContext = dbContext;
            _analyzerService = analyzerService;
            _fileReader = fileReader;
            _hashService = hashService;
            _logger = logger;
        }

        public async Task ExecuteAsync(FileBatchAnalysisMessage message, CancellationToken ct = default)
        {
            _logger.LogInformation("Starting to process batch of {Count} files for Session {SessionId}. Starts with: {FirstFile}", message.FilePaths.Count(), message.SessionId, message.FilePaths.FirstOrDefault());

            
            ParallelOptions parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            };

            ConcurrentBag<(string Path, string Hash, string Text)> fileData = await ReadFilesAsync(message.FilePaths, parallelOptions);

            Dictionary<string?, FileEntity> cachedFiles = await GetCachedFilesAsync(fileData, ct);

            ConcurrentBag<FileAnalysisResult> results = await AnalyzeTextsAsync(fileData, cachedFiles, parallelOptions);

            await SaveResultsToDatabaseAsync(message.SessionId, fileData, cachedFiles, results, ct);

            _logger.LogInformation("Successfully processed batch of {Count} files for Session {SessionId}. Cache hits: {CacheHits}", message.FilePaths.Count(), message.SessionId, cachedFiles.Count);

        }

        private async Task<ConcurrentBag<(string Path, string Hash, string Text)>> ReadFilesAsync(
            IEnumerable<string> filePaths, ParallelOptions parallelOptions)
        {
            ConcurrentBag<(string Path, string Hash, string Text)> fileData = new ConcurrentBag<(string Path, string Hash, string Text)>();

            await Parallel.ForEachAsync(filePaths, parallelOptions, async (filePath, token) =>
            {
                try
                {
                    string text = await _fileReader.ReadAllTextAsync(filePath, token);
                    string hash = _hashService.ComputeSha256Hash(text);
                    fileData.Add((filePath, hash, text));
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogError(ex, "Access denied while reading file: {FilePath}", filePath);
                }
                catch (IOException ex)
                {
                    _logger.LogError(ex, "Could not read file: {FilePath}", filePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred while reading file: {FilePath}", filePath);
                }
            });

            return fileData;
        }

        private async Task<Dictionary<string?, FileEntity>> GetCachedFilesAsync(
            ConcurrentBag<(string Path, string Hash, string Text)> fileData, CancellationToken ct)
        {
            List<string> allHashes = fileData.Select(x => x.Hash).ToList();
            List<FileEntity> cachedFilesList = await _dbContext.Files
                .Include(f => f.Result)
                .Where(f => allHashes.Contains(f.FileHash))
                .ToListAsync(ct);

            // Group by hash and take the first item to safely build a dictionary.
            // This acts as a defensive mechanism against legacy duplicate records 
            // in the database that share the same FileHash, preventing ArgumentException 
            // (duplicate keys) during ToDictionary execution.
            Dictionary<string?, FileEntity> cachedFiles = cachedFilesList
                .GroupBy(f => f.FileHash)
                .ToDictionary(g => g.Key, g => g.First());

            return cachedFiles;
        }

        private async Task<ConcurrentBag<FileAnalysisResult>> AnalyzeTextsAsync(
            ConcurrentBag<(string Path, string Hash, string Text)> fileData,
            Dictionary<string?, FileEntity> cachedFiles,
            ParallelOptions parallelOptions)
        {
            ConcurrentBag<FileAnalysisResult> results = new ConcurrentBag<FileAnalysisResult>();

            await Parallel.ForEachAsync(fileData, parallelOptions, async (data, ct) =>
            {
                try
                {
                    TextAnalysisResult result;

                    if (cachedFiles.TryGetValue(data.Hash, out FileEntity? cachedEntity))
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred while analyzing file: {FilePath}", data.Path);
                }
            });

            return results;
        }

        private async Task SaveResultsToDatabaseAsync(
            Guid sessionId,
            ConcurrentBag<(string Path, string Hash, string Text)> fileData,
            Dictionary<string?, FileEntity> cachedFiles,
            ConcurrentBag<FileAnalysisResult> results,
            CancellationToken ct)
        {
            SessionEntity? session = await _dbContext.Sessions
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found in DB. Skipping batch.", sessionId);
                return;
            }

            foreach ((string Path, string Hash, string Text) data in fileData)
            {
                if (cachedFiles.TryGetValue(data.Hash, out FileEntity? existingFile))
                {
                    session.Files.Add(existingFile);
                }
                else
                {
                    TextAnalysisResult analysisResult = results.First(r => r.Hash == data.Hash).AnalysisResult;

                    FileEntity newFile = new FileEntity
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
                    _dbContext.Files.Add(newFile);
                    session.Files.Add(newFile);
                }
            }

            session.FinishedAt = DateTime.UtcNow; // a little dirty but works for now

            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
