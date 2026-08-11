using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Services;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;
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
            var options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
                .UseNpgsql(_fixture.GetConnectionString())
                .Options;

            using var dbContext = new TextAnalyzerDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            dbContext.Sessions.RemoveRange(dbContext.Sessions);
            dbContext.Files.RemoveRange(dbContext.Files);
            await dbContext.SaveChangesAsync();

            var dirReader = new LocalDirectoryReader(
                Microsoft.Extensions.Options.Options.Create(new TextAnalyzer.Infrastructure.Settings.ReaderSettings
                {
                    AllowedExtensions = new[] { ".txt" }
                }));
            var fileReader = new LocalFileReader();
            var analyzer = new TextAnalyzerService();
            var writer = new CsvFileAnalysisResultWriter();
            var logger = NullLogger<AnalyzeFolderUseCase>.Instance;
            var hashService = new HashService();
            var useCase = new AnalyzeFolderUseCase(dirReader, fileReader, analyzer, writer, dbContext, logger, hashService);

            using var tempDir = new TempDirectoryHelper();

            await useCase.Execute(tempDir.FolderPath, default);

            var savedSession = await dbContext.Sessions
                .Include(s => s.Files)
                .ThenInclude(f => f.Result)
                .SingleOrDefaultAsync();

            Assert.NotNull(savedSession);
            Assert.Equal(2, savedSession.Files.Count);

            var results = savedSession.Files.Select(f => f.Result).ToList();
            Assert.Contains(results, r => r.LongestWord == "Coooooooooooooooooooooooooontent");
            Assert.Contains(results, r => r.LineCount == 3);
            Assert.Equal(2, results.Count);

            Assert.True(File.Exists(Path.Combine(tempDir.FolderPath, "results.csv")));
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

            var dirReader = new LocalDirectoryReader(
                Microsoft.Extensions.Options.Options.Create(new TextAnalyzer.Infrastructure.Settings.ReaderSettings
                {
                    AllowedExtensions = new[] { ".txt" }
                }));

            var fileReader = new LocalFileReader();
            var mockAnalyzer = new Mock<ITextAnalyzerService>();
            var writer = new CsvFileAnalysisResultWriter();
            var logger = NullLogger<AnalyzeFolderUseCase>.Instance;
            var hashService = new HashService();

            var useCase = new AnalyzeFolderUseCase(dirReader, fileReader, mockAnalyzer.Object, writer, dbContext, logger, hashService);
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
