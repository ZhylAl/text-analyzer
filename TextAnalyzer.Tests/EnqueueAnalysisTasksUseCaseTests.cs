using Microsoft.EntityFrameworkCore;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Infrastructure.Data;

namespace TextAnalyzer.Tests;

public class EnqueueAnalysisTasksUseCaseTests
{
    private readonly TextAnalyzerDbContext _dbContext;
    private readonly Mock<IMessageProducer> _mockMessageProducer;
    private readonly EnqueueAnalysisTasksUseCase _useCase;

    public EnqueueAnalysisTasksUseCaseTests()
    {
        DbContextOptions<TextAnalyzerDbContext> options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new TextAnalyzerDbContext(options);

        _mockMessageProducer = new Mock<IMessageProducer>();

        _useCase = new EnqueueAnalysisTasksUseCase(_dbContext, _mockMessageProducer.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateSessionAndPublishMessagesInChunks()
    {
        // Arrange
        List<string> filePaths = Enumerable.Range(1, 1200).Select(i => $"file{i}.txt").ToList();

        // Act
        Guid sessionId = await _useCase.ExecuteAsync(filePaths, CancellationToken.None);

        // Assert
        SessionEntity? savedSession = await _dbContext.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId);
        Assert.NotNull(savedSession);
        Assert.Equal((int)ExecutionMode.Folder, savedSession.ExecutionModeId);

        // checking chunking, the message producer should be called 3 times (500 + 500 + 200)
        _mockMessageProducer.Verify(
            p => p.PublishFileAnalysisRequestAsync(
                It.Is<IEnumerable<string>>(chunk => chunk.Count() <= 500), 
                sessionId, 
                It.IsAny<CancellationToken>()), 
            Times.Exactly(3));
    }
}
