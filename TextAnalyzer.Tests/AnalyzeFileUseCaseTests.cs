using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;

namespace TextAnalyzer.Tests;

public class AnalyzeFileUseCaseTests
{
    private readonly Mock<IFileReader> _mockFileReader;
    private readonly Mock<ITextAnalyzerService> _mockAnalyzerService;
    private readonly TextAnalyzerDbContext _dbContext;
    private readonly Mock<ILogger<AnalyzeFileUseCase>> _mockLogger;
    private readonly Mock<IHashService> _mockHashService;
    private readonly AnalyzeFileUseCase _useCase;

    public AnalyzeFileUseCaseTests()
    {
        _mockFileReader = new Mock<IFileReader>();
        _mockAnalyzerService = new Mock<ITextAnalyzerService>();
        _mockLogger = new Mock<ILogger<AnalyzeFileUseCase>>();
        _mockHashService = new Mock<IHashService>();

        DbContextOptions<TextAnalyzerDbContext> options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new TextAnalyzerDbContext(options);

        _useCase = new AnalyzeFileUseCase(
            _mockFileReader.Object,
            _mockAnalyzerService.Object,
            _dbContext,
            _mockLogger.Object,
            _mockHashService.Object);
    }

    [Fact]
    public async Task Execute_ShouldReadAnalyzeAndSaveSession()
    {
        // Arrange
        string filePath = "test.txt";
        string fileContent = "Hello test world";
        TextAnalysisResult expectedAnalysisResult = new TextAnalysisResult(16, 3, 1, "world");

        _mockFileReader
            .Setup(f => f.ReadAllTextAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);

        _mockAnalyzerService
            .Setup(a => a.Analyze(fileContent))
            .Returns(expectedAnalysisResult);

        _mockHashService
            .Setup(h => h.ComputeSha256Hash(fileContent))
            .Returns("testhash123");


        // Act
        TextAnalysisResult result = await _useCase.Execute(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAnalysisResult.CharCount, result.CharCount);
        Assert.Equal(expectedAnalysisResult.WordCount, result.WordCount);
        Assert.Equal(expectedAnalysisResult.LineCount, result.LineCount);
        Assert.Equal(expectedAnalysisResult.LongestWord, result.LongestWord);

        SessionEntity? savedSession = await _dbContext.Sessions
            .Include(s => s.Files)
            .ThenInclude(f => f.Result)
            .SingleOrDefaultAsync();

        Assert.NotNull(savedSession);
        Assert.Equal((int)ExecutionMode.SingleFile, savedSession.ExecutionModeId);
        Assert.Single(savedSession.Files);

        FileEntity savedFile = savedSession.Files.First();
        Assert.Equal(filePath, savedFile.FilePath);
        Assert.Equal("testhash123", savedFile.FileHash);

        Assert.NotNull(savedFile.Result); 
        Assert.Equal(expectedAnalysisResult.CharCount, savedFile.Result.CharCount);
        Assert.Equal(expectedAnalysisResult.WordCount, savedFile.Result.WordCount);

        _mockAnalyzerService.Verify(a => a.Analyze(fileContent), Times.Once);

    }

    [Fact]
    public async Task Execute_ShouldUseCachedResult_WhenHashExists()
    {
        // Arrange
        string filePath = "test.txt";
        string fileContent = "Hello test world";
        TextAnalysisResult expectedAnalysisResult = new TextAnalysisResult(16, 3, 1, "world");

        _mockFileReader
            .Setup(f => f.ReadAllTextAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);

        FileEntity existingFile = new FileEntity
        {
            Id = Guid.NewGuid(),
            FilePath = "test.txt",
            FileHash = "testhash123",
            Result = new ResultEntity { Id = Guid.NewGuid(), CharCount = 16, WordCount = 3, LineCount = 1, LongestWord = "world" }
        };
        _dbContext.Files.Add(existingFile);
        await _dbContext.SaveChangesAsync();

        _mockHashService
            .Setup(h => h.ComputeSha256Hash(fileContent))
            .Returns("testhash123");

        // Act
        TextAnalysisResult result = await _useCase.Execute(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAnalysisResult.CharCount, result.CharCount);
        Assert.Equal(expectedAnalysisResult.WordCount, result.WordCount);
        Assert.Equal(expectedAnalysisResult.LineCount, result.LineCount);
        Assert.Equal(expectedAnalysisResult.LongestWord, result.LongestWord);

        SessionEntity? savedSession = await _dbContext.Sessions
            .Include(s => s.Files)
            .ThenInclude(f => f.Result)
            .SingleOrDefaultAsync();

        Assert.NotNull(savedSession);
        Assert.Equal((int)ExecutionMode.SingleFile, savedSession.ExecutionModeId);
        Assert.Single(savedSession.Files);

        FileEntity savedFile = savedSession.Files.First();
        Assert.Equal(filePath, savedFile.FilePath);
        Assert.Equal("testhash123", savedFile.FileHash);

        Assert.NotNull(savedFile.Result);
        Assert.Equal(expectedAnalysisResult.CharCount, savedFile.Result.CharCount);

        _mockAnalyzerService.Verify(a => a.Analyze(fileContent), Times.Never);
    }

    [Fact]
    public async Task Execute_ShouldThrowException_IfFileReaderThrows()
    {
        // Arrange
        string filePath = "missing.txt";

        _mockFileReader
            .Setup(f => f.ReadAllTextAsync(filePath, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("File not found"));

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _useCase.Execute(filePath, CancellationToken.None));

        // Ensure that db was never called because it failed early
        Assert.Empty(_dbContext.Sessions);
    }
}
