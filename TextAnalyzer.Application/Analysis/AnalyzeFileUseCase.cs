using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Analysis;

public class AnalyzeFileUseCase
{
    private readonly IFileReader _fileReader;
    private readonly ITextAnalyzerService _analyzerService;
    private readonly IApplicationDbContext _dbContext;
    private readonly ILogger<AnalyzeFileUseCase> _logger; 
    private readonly IHashService _hashService;

    public AnalyzeFileUseCase(IFileReader fileReader, ITextAnalyzerService analyzerService, IApplicationDbContext dbContext, ILogger<AnalyzeFileUseCase> logger, IHashService hashService)
    {
        _fileReader = fileReader;
        _analyzerService = analyzerService;
        _dbContext = dbContext;
        _logger = logger;
        _hashService = hashService; 
    }

    public async Task<TextAnalysisResult> Execute(string filePath, CancellationToken ct)
    {
        _logger.LogInformation("Starting analysis for file: {FilePath}", filePath);

        DateTime startedAt = DateTime.UtcNow;
        string text = await _fileReader.ReadAllTextAsync(filePath, ct);

        string fileHash = _hashService.ComputeSha256Hash(text);

        TextAnalysisResult result;

        FileEntity? cachedFile = await _dbContext.Files
            .Include(f => f.Result) 
            .FirstOrDefaultAsync(f => f.FileHash == fileHash, ct);  

        if (cachedFile != null)
        {
            _logger.LogInformation("Found cached result for file: {FilePath}", filePath);
            result = new TextAnalysisResult(
                cachedFile.Result.CharCount,
                cachedFile.Result.WordCount,
                cachedFile.Result.LineCount,
                cachedFile.Result.LongestWord
            );
        }
        else
        {
             result = _analyzerService.Analyze(text);
        }

        SessionEntity session = new SessionEntity
        {
            Id = Guid.NewGuid(),
            StartedAt = startedAt,
            FinishedAt = DateTime.UtcNow,
            ExecutionModeId = (int)ExecutionMode.SingleFile,
            Files = new List<FileEntity>()
        };

        if (cachedFile != null)
        {
            session.Files.Add(cachedFile);
        }
        else
        {
            FileEntity newFile = new FileEntity
            {
                Id = Guid.NewGuid(),
                FilePath = filePath,
                FileHash = fileHash,
                Result = new ResultEntity
                {
                    Id = Guid.NewGuid(),
                    CharCount = result.CharCount,
                    WordCount = result.WordCount,
                    LineCount = result.LineCount,
                    LongestWord = result.LongestWord
                }
            };
            session.Files.Add(newFile);
        }

        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Analysis finished for file: {FilePath} with Hash: {Hash}. Found {WordCount} words...", filePath, fileHash, result.WordCount);
        return result;
    }
}