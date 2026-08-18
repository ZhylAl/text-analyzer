using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;

namespace TextAnalyzer.Tests;

public class ProcessBatchUseCaseTests
{
    private readonly TextAnalyzerDbContext _dbContext;
    private readonly Mock<ITextAnalyzerService> _mockAnalyzerService;
    private readonly Mock<IFileReader> _mockFileReader;
    private readonly Mock<IHashService> _mockHashService;
    private readonly Mock<ILogger<ProcessBatchUseCase>> _mockLogger;
    private readonly ProcessBatchUseCase _useCase;

    public ProcessBatchUseCaseTests()
    {
        DbContextOptions<TextAnalyzerDbContext> options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TextAnalyzerDbContext(options);

        _mockAnalyzerService = new Mock<ITextAnalyzerService>();
        _mockFileReader = new Mock<IFileReader>();
        _mockHashService = new Mock<IHashService>();
        _mockLogger = new Mock<ILogger<ProcessBatchUseCase>>();

        _useCase = new ProcessBatchUseCase(
            _dbContext,
            _mockAnalyzerService.Object,
            _mockFileReader.Object,
            _mockHashService.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    public async Task ExecuteAsync_ShouldProcessNewFilesAndSaveToDb()
    {
        // Arrange
        Guid sessionId = Guid.NewGuid();
        SessionEntity session = new SessionEntity { Id = sessionId, ExecutionModeId = 1 };
        _dbContext.Sessions.Add(session);
        await _dbContext.SaveChangesAsync();

        FileBatchAnalysisMessage message = new FileBatchAnalysisMessage(sessionId, new[] { "file1.txt" });

        _mockFileReader.Setup(f => f.ReadAllTextAsync("file1.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync("test text");
            
        _mockHashService.Setup(h => h.ComputeSha256Hash("test text"))
            .Returns("hash1");

        _mockAnalyzerService.Setup(a => a.Analyze("test text"))
            .Returns(new TextAnalysisResult(9, 2, 1, "text"));

        // Act
        await _useCase.ExecuteAsync(message, CancellationToken.None);

        // Assert
        SessionEntity? updatedSession = await _dbContext.Sessions.Include(s => s.Files).ThenInclude(f => f.Result).SingleOrDefaultAsync(s => s.Id == sessionId);
        Assert.NotNull(updatedSession);
        Assert.Single(updatedSession.Files);
        FileEntity file = updatedSession.Files.First();
        Assert.Equal("file1.txt", file.FilePath);
        Assert.Equal("hash1", file.FileHash);
        Assert.NotNull(file.Result);
        Assert.Equal(9, file.Result.CharCount);
        Assert.Equal(2, file.Result.WordCount);
        Assert.Equal(1, file.Result.LineCount);
        Assert.Equal("text", file.Result.LongestWord);
        Assert.NotNull(updatedSession.FinishedAt);
    }
    
    [Fact]
    public async Task ExecuteAsync_ShouldUseCachedResult_WhenFileHashMatches()
    {
        // Arrange
        Guid sessionId = Guid.NewGuid();
        SessionEntity session = new SessionEntity { Id = sessionId, ExecutionModeId = 1 };
        _dbContext.Sessions.Add(session);
        
        FileEntity existingFile = new FileEntity
        {
            Id = Guid.NewGuid(),
            FilePath = "old_file.txt",
            FileHash = "cached_hash",
            Result = new ResultEntity
            {
                Id = Guid.NewGuid(),
                CharCount = 10,
                WordCount = 3,
                LineCount = 1,
                LongestWord = "cached"
            }
        };
        _dbContext.Files.Add(existingFile);
        await _dbContext.SaveChangesAsync();

        FileBatchAnalysisMessage message = new FileBatchAnalysisMessage(sessionId, new[] { "file2.txt" });

        _mockFileReader.Setup(f => f.ReadAllTextAsync("file2.txt", It.IsAny<CancellationToken>()))
            .ReturnsAsync("cached text");
            
        _mockHashService.Setup(h => h.ComputeSha256Hash("cached text"))
            .Returns("cached_hash");

        // Act
        await _useCase.ExecuteAsync(message, CancellationToken.None);

        // Assert
        _mockAnalyzerService.Verify(a => a.Analyze(It.IsAny<string>()), Times.Never); // Should not call analyzer
        
        SessionEntity? updatedSession = await _dbContext.Sessions.Include(s => s.Files).ThenInclude(f => f.Result).SingleOrDefaultAsync(s => s.Id == sessionId);
        Assert.NotNull(updatedSession);
        Assert.Single(updatedSession.Files);
        FileEntity file = updatedSession.Files.First();
        Assert.Equal(existingFile.Id, file.Id); // Uses existing file entity
    }
}
