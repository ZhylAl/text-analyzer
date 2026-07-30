using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;

namespace TextAnalyzer.Tests;

public class AnalyzeFolderUseCaseTests
{
    private readonly Mock<IDirectoryReader> _mockDirectoryReader;
    private readonly Mock<IFileReader> _mockFileReader;
    private readonly Mock<ITextAnalyzerService> _mockAnalyzerService;
    private readonly Mock<IFileAnalysisResultWriter> _mockResultWriter;
    private readonly TextAnalyzerDbContext _dbContext;
    private readonly Mock<ILogger<AnalyzeFolderUseCase>> _mockLogger;
    private readonly Mock<IHashService> _mockHashService;

    private readonly AnalyzeFolderUseCase _useCase;

    public AnalyzeFolderUseCaseTests()
    {
        _mockDirectoryReader = new Mock<IDirectoryReader>();
        _mockFileReader = new Mock<IFileReader>();
        _mockAnalyzerService = new Mock<ITextAnalyzerService>();
        _mockResultWriter = new Mock<IFileAnalysisResultWriter>();
        _mockLogger = new Mock<ILogger<AnalyzeFolderUseCase>>();
        _mockHashService = new Mock<IHashService>();

        var options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TextAnalyzerDbContext(options);

        _useCase = new AnalyzeFolderUseCase(
            _mockDirectoryReader.Object,
            _mockFileReader.Object,
            _mockAnalyzerService.Object,
            _mockResultWriter.Object,
            _dbContext,
            _mockLogger.Object,
            _mockHashService.Object);
    }

    [Fact]
    public async Task Execute_ShouldProcessFilesAndReturnLongestWord()
    {
        // Arrange
        string folderPath = "FakeFolder";
        var filePaths = new[] { Path.Combine(folderPath, "file1.txt"), Path.Combine(folderPath, "file2.txt") };

        _mockDirectoryReader.Setup(d => d.GetTextFiles(folderPath)).Returns(filePaths);

        _mockFileReader
            .Setup(f => f.ReadAllTextAsync(Path.Combine(folderPath, "file1.txt"), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Hello world");        
        
        _mockFileReader
            .Setup(f => f.ReadAllTextAsync(Path.Combine(folderPath, "file2.txt"), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Short text");

        _mockAnalyzerService.Setup(a => a.Analyze("Hello world"))
            .Returns(new TextAnalysisResult(11, 2, 1, "world"));

        _mockAnalyzerService.Setup(a => a.Analyze("Short text"))
            .Returns(new TextAnalysisResult(10, 2, 1, "text"));

        IEnumerable<FileAnalysisExportDto> capturedExportDtos = null;
        _mockResultWriter.Setup(w => w.WriteResults(It.IsAny<string>(), It.IsAny<IEnumerable<FileAnalysisExportDto>>()))
            .Callback<string, IEnumerable<FileAnalysisExportDto>>((path, dtos) => capturedExportDtos = dtos.ToList());

        _mockHashService.Setup(h => h.ComputeSha256Hash("Hello world")).Returns("hash1");
        _mockHashService.Setup(h => h.ComputeSha256Hash("Short text")).Returns("hash2");

        // Act
        AnalyzeFolderResponse result = await _useCase.Execute(folderPath);

        // Assert
        Assert.Equal("world", result.LongestWordOverall);
        
        string expectedCsvPath = Path.Combine(folderPath, "results.csv");
        _mockResultWriter.Verify(w => w.WriteResults(It.Is<string>(p => p == expectedCsvPath), It.IsAny<IEnumerable<FileAnalysisExportDto>>()), Times.Once);
        
        Assert.NotNull(capturedExportDtos);
        Assert.Equal(2, capturedExportDtos.Count());
        Assert.Contains(capturedExportDtos, d => d.FileName == "file1.txt" && d.LongestWord == "world");
        Assert.Contains(capturedExportDtos, d => d.FileName == "file2.txt" && d.LongestWord == "text");

        var savedSession = await _dbContext.Sessions.Include(s => s.Files).SingleOrDefaultAsync();
        Assert.NotNull(savedSession);
        Assert.Equal((int)ExecutionMode.Folder, savedSession.ExecutionModeId);
        Assert.Equal(2, savedSession.Files.Count); 

        _mockAnalyzerService.Verify(a => a.Analyze(It.IsAny<string>()), Times.Exactly(2));

    }

    [Fact]
    public async Task Execute_EmptyFolder_ShouldReturnEmptyString()
    {
        // Arrange
        string folderPath = "EmptyFolder";
        _mockDirectoryReader.Setup(d => d.GetTextFiles(folderPath)).Returns(System.Array.Empty<string>());

        // Act
        AnalyzeFolderResponse result = await _useCase.Execute(folderPath);

        // Assert
        Assert.Equal(string.Empty, result.LongestWordOverall);
        _mockResultWriter.Verify(w => w.WriteResults(It.IsAny<string>(), It.Is<IEnumerable<FileAnalysisExportDto>>(dtos => !dtos.Any())), Times.Once);
        var savedSession = await _dbContext.Sessions.Include(s => s.Files).SingleOrDefaultAsync();
        Assert.NotNull(savedSession);
        Assert.Empty(savedSession.Files);
    }

    [Fact]
    public async Task Execute_ShouldSkipFilesThatThrowExceptions()
    {
        // Arrange
        string folderPath = "FakeFolder";
        var filePaths = new[] { Path.Combine(folderPath, "bad.txt"), Path.Combine(folderPath, "good.txt") };
        
        _mockDirectoryReader.Setup(d => d.GetTextFiles(folderPath)).Returns(filePaths);

        _mockFileReader.Setup(f => f.ReadAllTextAsync(Path.Combine(folderPath, "bad.txt"), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new System.Exception("File locked"));
        _mockFileReader.Setup(f => f.ReadAllTextAsync(Path.Combine(folderPath, "good.txt"), It.IsAny<CancellationToken>()))
            .ReturnsAsync("good text");

        _mockAnalyzerService.Setup(a => a.Analyze("good text"))
            .Returns(new TextAnalysisResult(9, 2, 1, "good"));

        _mockHashService.Setup(h => h.ComputeSha256Hash("good text")).Returns("hash_good");

        IEnumerable<FileAnalysisExportDto> capturedExportDtos = null;
        _mockResultWriter.Setup(w => w.WriteResults(It.IsAny<string>(), It.IsAny<IEnumerable<FileAnalysisExportDto>>()))
            .Callback<string, IEnumerable<FileAnalysisExportDto>>((path, dtos) => capturedExportDtos = dtos.ToList());

        // Act
        AnalyzeFolderResponse result = await _useCase.Execute(folderPath);

        // Assert
        Assert.Equal("good", result.LongestWordOverall);
        Assert.NotNull(capturedExportDtos);
        Assert.Single(capturedExportDtos);
        Assert.Equal("good.txt", capturedExportDtos.First().FileName);

        var savedSession = await _dbContext.Sessions.Include(s => s.Files).SingleOrDefaultAsync();
        Assert.NotNull(savedSession);
        Assert.Single(savedSession.Files); 

    }
}
