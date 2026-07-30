using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Services;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;
using TextAnalyzer.Infrastructure.Reader;

namespace TextAnalyzer.IntegrationTests;

public class AnalyzeFileUseCaseIntegrationTests : IClassFixture<PostgreSqlDatabaseFixture>
{
    private readonly PostgreSqlDatabaseFixture _fixture;

    public AnalyzeFileUseCaseIntegrationTests(PostgreSqlDatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Execute_ShouldAnalyzeRealFile_AndSaveToRealDb()
    {
        var options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseNpgsql(_fixture.GetConnectionString())
            .Options;

        using var dbContext = new TextAnalyzerDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Sessions.RemoveRange(dbContext.Sessions);
        dbContext.Files.RemoveRange(dbContext.Files);
        await dbContext.SaveChangesAsync();

        var reader = new LocalFileReader();
        var analyzer = new TextAnalyzerService();
        var logger = NullLogger<AnalyzeFileUseCase>.Instance;
        var hashService = new HashService();
        var useCase = new AnalyzeFileUseCase(reader, analyzer, dbContext, logger, hashService); 

        using var tempFile = new TempFileHelper("Hello integration test world");

        await useCase.Execute(tempFile.FilePath, default);

        var savedSession = await dbContext.Sessions
            .Include(s => s.Files)
            .ThenInclude(f => f.Result)
            .FirstOrDefaultAsync();

        Assert.Equal(1, await dbContext.Sessions.CountAsync());
        Assert.NotNull(savedSession);
        Assert.Single(savedSession.Files);
        Assert.Equal(1, savedSession.Files.Count());
        Assert.Equal("integration", savedSession.Files.First().Result.LongestWord);
    } 
    
    [Fact] 
    public async Task Execute_ShouldUseCache_OnSubsequentRuns()
    {
        var options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseNpgsql(_fixture.GetConnectionString())
            .Options;
        using var dbContext = new TextAnalyzerDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Sessions.RemoveRange(dbContext.Sessions);
        dbContext.Files.RemoveRange(dbContext.Files);
        await dbContext.SaveChangesAsync();

        var reader = new LocalFileReader();
        var mockAnalyzer = new Mock<ITextAnalyzerService>();
        var logger = NullLogger<AnalyzeFileUseCase>.Instance;
        var hashService = new HashService();

        var useCase = new AnalyzeFileUseCase(reader, mockAnalyzer.Object, dbContext, logger, hashService);

        mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>()))
            .Returns(new TextAnalysisResult(1, 1, 1, "test"));

        using var tempFile = new TempFileHelper("Hello integration test world");

        await useCase.Execute(tempFile.FilePath, default);
        await useCase.Execute(tempFile.FilePath, default);

        mockAnalyzer.Verify(a => a.Analyze(It.IsAny<string>()), Times.Once);
    }
}
