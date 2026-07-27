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
using TextAnalyzer.Infrastructure.Export;
using TextAnalyzer.Infrastructure.Reader;

namespace TextAnalyzer.IntegrationTests
{
    public class AnalyzeFolderUseCaseIntegrationTests : IClassFixture<PostgreSqlDatabaseFixture>
    {
        private readonly PostgreSqlDatabaseFixture _fixture;

        public AnalyzeFolderUseCaseIntegrationTests(PostgreSqlDatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Execute_ShouldAnalyzeRealFolder_AndSaveToRealDb()
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
            var dirReader = new LocalDirectoryReader(new[] { ".txt" }); // Specify allowed extensions
            var fileReader = new LocalFileReader();
            var analyzer = new TextAnalyzerService();
            var writer = new CsvFileAnalysisResultWriter();
            var logger = NullLogger<AnalyzeFolderUseCase>.Instance;
            var hashService = new HashService();
            var useCase = new AnalyzeFolderUseCase(dirReader, fileReader, analyzer, writer, repository, logger, hashService);

            using var tempDir = new TempDirectoryHelper();

            await useCase.Execute(tempDir.FolderPath, default);

            var savedSession = await setupContext.Sessions.Include(s => s.Results).Include(s => s.Files).FirstOrDefaultAsync();
            Assert.Equal(1, await setupContext.Sessions.CountAsync());
            Assert.NotNull(savedSession);
            Assert.Contains(savedSession.Results, r => r.LongestWord == "Coooooooooooooooooooooooooontent");
            Assert.Contains(savedSession.Results, r => r.LineCount == 3);
            Assert.Equal(2, savedSession.Files.Count());
            Assert.Equal(2, savedSession.Results.Count());
            Assert.True(File.Exists(Path.Combine(tempDir.FolderPath, "results.csv")));
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
            var dirReader = new LocalDirectoryReader(new[] { ".txt" }); // Specify allowed extensions
            var fileReader = new LocalFileReader();
            var mockAnalyzer = new Mock<ITextAnalyzerService>();
            var writer = new CsvFileAnalysisResultWriter();
            var logger = NullLogger<AnalyzeFolderUseCase>.Instance;
            var hashService = new HashService();
            var useCase = new AnalyzeFolderUseCase(dirReader, fileReader, mockAnalyzer.Object, writer, repository, logger, hashService);

            mockAnalyzer.Setup(a => a.Analyze(It.IsAny<string>()))
                .Returns(new TextAnalysisResult(1, 1, 1, "test"));

            using var tempDir = new TempDirectoryHelper();

            await useCase.Execute(tempDir.FolderPath, default);
            await useCase.Execute(tempDir.FolderPath, default);

            // Should be just 2 time - because in the 2nd run, the results should be fetched from the database and not analyzed again
            mockAnalyzer.Verify(a => a.Analyze(It.IsAny<string>()), Times.Exactly(2)); 
        }
    }
}
