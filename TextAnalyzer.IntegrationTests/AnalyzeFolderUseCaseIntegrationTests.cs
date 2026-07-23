using Microsoft.EntityFrameworkCore;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Domain.Services;
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
            var options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
                .UseNpgsql(_fixture.GetConnectionString())
                .Options;

            using var context = new TextAnalyzerDbContext(options);
            await context.Database.EnsureCreatedAsync();

            var repository = new SessionRepository(context);
            var dirReader = new LocalDirectoryReader(new[] { ".txt" }); // Specify allowed extensions
            var fileReader = new LocalFileReader();
            var analyzer = new TextAnalyzerService();
            var writer = new CsvFileAnalysisResultWriter();
            var useCase = new AnalyzeFolderUseCase(dirReader, fileReader, analyzer, writer, repository);

            using var tempDir = new TempDirectoryHelper();

            await useCase.Execute(tempDir.FolderPath, default);

            var savedSession = await context.Sessions.Include(s => s.Results).Include(s => s.Files).FirstOrDefaultAsync();
            Assert.Equal(1, await context.Sessions.CountAsync());
            Assert.NotNull(savedSession);
            Assert.Contains(savedSession.Results, r => r.LongestWord == "Coooooooooooooooooooooooooontent");
            Assert.Contains(savedSession.Results, r => r.LineCount == 3);
            Assert.Equal(2, savedSession.Files.Count());
            Assert.Equal(2, savedSession.Results.Count());
            Assert.True(File.Exists(Path.Combine(tempDir.FolderPath, "results.csv")));
        }
    }
}
