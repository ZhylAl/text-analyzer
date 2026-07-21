using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Domain.Services;

namespace TextAnalyzer.Tests;

public class AnalyzeFolderUseCaseTests
{
    private readonly Mock<IDirectoryReader> _mockDirectoryReader;
    private readonly Mock<IFileReader> _mockFileReader;
    private readonly Mock<ITextAnalyzerService> _mockAnalyzerService;
    private readonly Mock<IFileAnalysisResultWriter> _mockResultWriter;
    private readonly Mock<ISessionRepository> _mockSessionRepository;
    private readonly AnalyzeFolderUseCase _useCase;

    public AnalyzeFolderUseCaseTests()
    {
        _mockDirectoryReader = new Mock<IDirectoryReader>();
        _mockFileReader = new Mock<IFileReader>();
        _mockAnalyzerService = new Mock<ITextAnalyzerService>();
        _mockResultWriter = new Mock<IFileAnalysisResultWriter>();
        _mockSessionRepository = new Mock<ISessionRepository>();

        _useCase = new AnalyzeFolderUseCase(
            _mockDirectoryReader.Object,
            _mockFileReader.Object,
            _mockAnalyzerService.Object,
            _mockResultWriter.Object,
            _mockSessionRepository.Object);
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

        // Act
        string result = await _useCase.Execute(folderPath);

        // Assert
        Assert.Equal("world", result);
        
        string expectedCsvPath = Path.Combine(folderPath, "results.csv");
        _mockResultWriter.Verify(w => w.WriteResults(It.Is<string>(p => p == expectedCsvPath), It.IsAny<IEnumerable<FileAnalysisExportDto>>()), Times.Once);
        
        Assert.NotNull(capturedExportDtos);
        Assert.Equal(2, capturedExportDtos.Count());
        Assert.Contains(capturedExportDtos, d => d.FileName == "file1.txt" && d.LongestWord == "world");
        Assert.Contains(capturedExportDtos, d => d.FileName == "file2.txt" && d.LongestWord == "text");

        _mockSessionRepository.Verify(repo => repo.AddAsync(It.Is<SessionSaveDto>(dto => 
            dto.ExecutionModeId == (int)ExecutionMode.Folder &&
            dto.Results.Count == 2
        )), Times.Once);
    }

    [Fact]
    public async Task Execute_EmptyFolder_ShouldReturnEmptyString()
    {
        // Arrange
        string folderPath = "EmptyFolder";
        _mockDirectoryReader.Setup(d => d.GetTextFiles(folderPath)).Returns(System.Array.Empty<string>());

        // Act
        string result = await _useCase.Execute(folderPath);

        // Assert
        Assert.Equal(string.Empty, result);
        _mockResultWriter.Verify(w => w.WriteResults(It.IsAny<string>(), It.Is<IEnumerable<FileAnalysisExportDto>>(dtos => !dtos.Any())), Times.Once);
        _mockSessionRepository.Verify(repo => repo.AddAsync(It.Is<SessionSaveDto>(dto => 
            dto.ExecutionModeId == (int)ExecutionMode.Folder &&
            !dto.Results.Any()
        )), Times.Once);
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

        IEnumerable<FileAnalysisExportDto> capturedExportDtos = null;
        _mockResultWriter.Setup(w => w.WriteResults(It.IsAny<string>(), It.IsAny<IEnumerable<FileAnalysisExportDto>>()))
            .Callback<string, IEnumerable<FileAnalysisExportDto>>((path, dtos) => capturedExportDtos = dtos.ToList());

        // Act
        string result = await _useCase.Execute(folderPath);

        // Assert
        Assert.Equal("good", result);
        Assert.NotNull(capturedExportDtos);
        Assert.Single(capturedExportDtos);
        Assert.Equal("good.txt", capturedExportDtos.First().FileName);
        
        _mockSessionRepository.Verify(repo => repo.AddAsync(It.Is<SessionSaveDto>(dto => 
            dto.ExecutionModeId == (int)ExecutionMode.Folder &&
            dto.Results.Count == 1
        )), Times.Once);
    }
}
