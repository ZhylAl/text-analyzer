using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Domain.Services;
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
        var options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
            .UseNpgsql(_fixture.GetConnectionString())
            .Options;

        using var context = new TextAnalyzerDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var repository = new SessionRepository(context);
        var reader = new LocalFileReader();
        var analyzer = new TextAnalyzerService();
        var logger = NullLogger<AnalyzeFileUseCase>.Instance;
        var useCase = new AnalyzeFileUseCase(reader, analyzer, repository, logger); 

        using var tempFile = new TempFileHelper("Hello integration test world");

        await useCase.Execute(tempFile.FilePath, default);


        var savedSession = await context.Sessions.Include(s => s.Results).Include(s => s.Files).FirstOrDefaultAsync();
        Assert.Equal(1, await context.Sessions.CountAsync());
        Assert.NotNull(savedSession);
        Assert.Single(savedSession.Results);
        Assert.Equal(1, savedSession.Files.Count());
        Assert.Equal("integration", savedSession.Results.First().LongestWord);
    }
}
