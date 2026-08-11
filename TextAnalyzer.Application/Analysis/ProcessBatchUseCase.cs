using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
            _logger.LogInformation("Starting to process batch of {Count} files for Session {SessionId}", message.FilePaths.Count(), message.SessionId);

            var results = new ConcurrentBag<FileAnalysisResult>();
            var fileData = new ConcurrentBag<(string Path, string Hash, string Text)>();

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(message.FilePaths, parallelOptions, async (filePath, token) =>
            {
                try
                {
                    var text = await _fileReader.ReadAllTextAsync(filePath, token);
                    var hash = _hashService.ComputeSha256Hash(text);
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

            await Parallel.ForEachAsync(fileData, parallelOptions, async (data, ct) =>
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unexpected error occurred while analyzing file: {FilePath}", data.Path);
                }
            });

            var session = await _dbContext.Sessions
                .Include(s => s.Files)
                .FirstOrDefaultAsync(s => s.Id == message.SessionId, ct);

            if (session == null)
            {
                _logger.LogWarning("Session {SessionId} not found in DB. Skipping batch.", message.SessionId);
                return;
            }

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

            session.FinishedAt = DateTime.UtcNow; // a little dirty but works for now
            _logger.LogInformation("Successfully processed batch of {Count} files for Session {SessionId}. Cache hits: {CacheHits}", message.FilePaths.Count(), message.SessionId, cachedFiles.Count);

            await _dbContext.SaveChangesAsync(ct);
        }
    }
}
