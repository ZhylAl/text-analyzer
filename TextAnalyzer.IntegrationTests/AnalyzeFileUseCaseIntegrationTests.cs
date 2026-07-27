using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Services;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;
using TextAnalyzer.Infrastructure.Data.Repositories;
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
        var services = new ServiceCollection()
                .AddDbContextFactory<TextAnalyzerDbContext>(opt => opt.UseNpgsql(_fixture.GetConnectionString()))
                .BuildServiceProvider();
        var factory = services.GetRequiredService<IDbContextFactory<TextAnalyzerDbContext>>();

        using var setupContext = factory.CreateDbContext();
        await setupContext.Database.EnsureCreatedAsync();

        setupContext.Sessions.RemoveRange(setupContext.Sessions);
        await setupContext.SaveChangesAsync();

        var repository = new SessionRepository(factory);
        var reader = new LocalFileReader();
        var analyzer = new TextAnalyzerService();
        var logger = NullLogger<AnalyzeFileUseCase>.Instance;
        var hashService = new HashService();
        var useCase = new AnalyzeFileUseCase(reader, analyzer, repository, logger, hashService); 

        using var tempFile = new TempFileHelper("Hello integration test world");

        await useCase.Execute(tempFile.FilePath, default);


        var savedSession = await setupContext.Sessions.Include(s => s.Results).Include(s => s.Files).FirstOrDefaultAsync();
        Assert.Equal(1, await setupContext.Sessions.CountAsync());
        Assert.NotNull(savedSession);
        Assert.Single(savedSession.Results);
        Assert.Equal(1, savedSession.Files.Count());
        Assert.Equal("integration", savedSession.Results.First().LongestWord);
    } 
    
    [Fact]
    public async Task Execute_ShouldUseCache_OnSubsequentRuns()
    {
        var services = new ServiceCollection()
                .AddDbContextFactory<TextAnalyzerDbContext>(opt => opt.UseNpgsql(_fixture.GetConnectionString()))
                .BuildServiceProvider();
        var factory = services.GetRequiredService<IDbContextFactory<TextAnalyzerDbContext>>();

        using var setupContext = factory.CreateDbContext();
        await setupContext.Database.EnsureCreatedAsync();

        setupContext.Sessions.RemoveRange(setupContext.Sessions);
        await setupContext.SaveChangesAsync();

        var repository = new SessionRepository(factory);
        var reader = new LocalFileReader();
        var mockAnalyzer = new Mock<ITextAnalyzerService>();
        var logger = NullLogger<AnalyzeFileUseCase>.Instance;
        var hashService = new HashService();
        var useCase = new AnalyzeFileUseCase(reader, mockAnalyzer.Object, repository, logger, hashService);

        mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>()))
            .Returns(new TextAnalysisResult(1, 1, 1, "test"));

        using var tempFile = new TempFileHelper("Hello integration test world");

        await useCase.Execute(tempFile.FilePath, default);
        await useCase.Execute(tempFile.FilePath, default);

        mockAnalyzer.Verify(a => a.Analyze(It.IsAny<string>()), Times.Once);
    }
}
