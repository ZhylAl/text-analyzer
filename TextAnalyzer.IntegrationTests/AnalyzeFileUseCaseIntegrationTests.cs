using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Services;
using TextAnalyzer.Domain.Entities;
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
        DbContextOptions<TextAnalyzerDbContext> options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseNpgsql(_fixture.GetConnectionString())
            .Options;

        using TextAnalyzerDbContext dbContext = new TextAnalyzerDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Sessions.RemoveRange(dbContext.Sessions);
        dbContext.Files.RemoveRange(dbContext.Files);
        await dbContext.SaveChangesAsync();

        LocalFileReader reader = new LocalFileReader();
        TextAnalyzerService analyzer = new TextAnalyzerService();
        NullLogger<AnalyzeFileUseCase> logger = NullLogger<AnalyzeFileUseCase>.Instance;
        HashService hashService = new HashService();
        AnalyzeFileUseCase useCase = new AnalyzeFileUseCase(reader, analyzer, dbContext, logger, hashService); 

        using TempFileHelper tempFile = new TempFileHelper("Hello integration test world");

        await useCase.Execute(tempFile.FilePath, default);

        SessionEntity? savedSession = await dbContext.Sessions
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
        DbContextOptions<TextAnalyzerDbContext> options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseNpgsql(_fixture.GetConnectionString())
            .Options;
        using TextAnalyzerDbContext dbContext = new TextAnalyzerDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Sessions.RemoveRange(dbContext.Sessions);
        dbContext.Files.RemoveRange(dbContext.Files);
        await dbContext.SaveChangesAsync();

        LocalFileReader reader = new LocalFileReader();
        Mock<ITextAnalyzerService> mockAnalyzer = new Mock<ITextAnalyzerService>();
        NullLogger<AnalyzeFileUseCase> logger = NullLogger<AnalyzeFileUseCase>.Instance;
        HashService hashService = new HashService();

        AnalyzeFileUseCase useCase = new AnalyzeFileUseCase(reader, mockAnalyzer.Object, dbContext, logger, hashService);

        mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>()))
            .Returns(new TextAnalysisResult(1, 1, 1, "test"));

        using TempFileHelper tempFile = new TempFileHelper("Hello integration test world");

        await useCase.Execute(tempFile.FilePath, default);
        await useCase.Execute(tempFile.FilePath, default);

        mockAnalyzer.Verify(a => a.Analyze(It.IsAny<string>()), Times.Once);
    }
}
