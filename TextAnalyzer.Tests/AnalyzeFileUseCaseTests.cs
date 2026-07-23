using Microsoft.Extensions.Logging;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Domain.Services;

namespace TextAnalyzer.Tests;

public class AnalyzeFileUseCaseTests
{
    private readonly Mock<IFileReader> _mockFileReader;
    private readonly Mock<ITextAnalyzerService> _mockAnalyzerService;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly Mock<ILogger<AnalyzeFileUseCase>> _mockLogger;
    private readonly AnalyzeFileUseCase _useCase;

    public AnalyzeFileUseCaseTests()
    {
        _mockFileReader = new Mock<IFileReader>();
        _mockAnalyzerService = new Mock<ITextAnalyzerService>();
        _mockSessionRepository = new Mock<ISessionRepository>();
        _mockLogger = new Mock<ILogger<AnalyzeFileUseCase>>();

        _useCase = new AnalyzeFileUseCase(
            _mockFileReader.Object,
            _mockAnalyzerService.Object,
            _mockSessionRepository.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task Execute_ShouldReadAnalyzeAndSaveSession()
    {
        // Arrange
        string filePath = "test.txt";
        string fileContent = "Hello test world";
        var expectedAnalysisResult = new TextAnalysisResult(16, 3, 1, "world");

        _mockFileReader
            .Setup(f => f.ReadAllTextAsync(filePath, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fileContent);

        _mockAnalyzerService
            .Setup(a => a.Analyze(fileContent))
            .Returns(expectedAnalysisResult);

        // Act
        var result = await _useCase.Execute(filePath, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedAnalysisResult.CharCount, result.CharCount);
        Assert.Equal(expectedAnalysisResult.WordCount, result.WordCount);
        Assert.Equal(expectedAnalysisResult.LineCount, result.LineCount);
        Assert.Equal(expectedAnalysisResult.LongestWord, result.LongestWord);

        _mockSessionRepository.Verify(repo => repo.AddAsync(It.Is<SessionEntity>(entity =>
            entity.ExecutionModeId == (int)ExecutionMode.SingleFile &&
            entity.Files.Count == 1 &&
            entity.Files.First().FilePath == filePath &&
            entity.Results.Count == 1 &&
            entity.Results.First().CharCount == expectedAnalysisResult.CharCount &&
            entity.Results.First().WordCount == expectedAnalysisResult.WordCount
        )), Times.Once);

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
        
        // Ensure that repository was never called because it failed early
        _mockSessionRepository.Verify(repo => repo.AddAsync(It.IsAny<SessionEntity>()), Times.Never);
    }
}
