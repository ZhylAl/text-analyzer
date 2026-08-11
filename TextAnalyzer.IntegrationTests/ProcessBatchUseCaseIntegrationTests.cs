using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;

namespace TextAnalyzer.IntegrationTests
{
    public class ProcessBatchUseCaseIntegrationTests : 
        IClassFixture<PostgreSqlDatabaseFixture>
    {
        private readonly PostgreSqlDatabaseFixture _dbFixture;

        public ProcessBatchUseCaseIntegrationTests(PostgreSqlDatabaseFixture dbFixture)
        {
            _dbFixture = dbFixture;
        }

        [Fact]
        public async Task ExecuteAsync_ShouldSaveResultsToDatabase()
        {
            var options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
                .UseNpgsql(_dbFixture.GetConnectionString())
                .Options;

            using (var setupContext = new TextAnalyzerDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync(); 
                
                // Clean up
                setupContext.Sessions.RemoveRange(setupContext.Sessions);
                setupContext.Files.RemoveRange(setupContext.Files);
                setupContext.Results.RemoveRange(setupContext.Results);
                await setupContext.SaveChangesAsync();
            }

            var sessionId = Guid.NewGuid();

            using (var arrangeContext = new TextAnalyzerDbContext(options))
            {
                var session = new SessionEntity { Id = sessionId, ExecutionModeId = 1 }; 
                arrangeContext.Sessions.Add(session);
                await arrangeContext.SaveChangesAsync();
            }

            var analyzerService = new TextAnalyzer.Application.Services.TextAnalyzerService();
            var fileReader = new TextAnalyzer.Infrastructure.Reader.LocalFileReader();
            var hashService = new TextAnalyzer.Application.Services.HashService();
            var logger = NullLogger<ProcessBatchUseCase>.Instance;

            // Create a real temporary file
            var tempFilePath = Path.GetTempFileName();
            var testText = "hello integration test world";
            await File.WriteAllTextAsync(tempFilePath, testText);

            try
            {
                using (var actContext = new TextAnalyzerDbContext(options))
                {
                    var useCase = new ProcessBatchUseCase(
                        actContext,
                        analyzerService,
                        fileReader,
                        hashService,
                        logger
                    );

                    var message = new FileBatchAnalysisMessage(sessionId, new[] { tempFilePath });

                    // Act
                    await useCase.ExecuteAsync(message, CancellationToken.None);
                }

                using (var assertContext = new TextAnalyzerDbContext(options))
                {
                    // Assert
                    var savedSession = await assertContext.Sessions
                        .Include(s => s.Files)
                        .ThenInclude(f => f.Result)
                        .SingleOrDefaultAsync(s => s.Id == sessionId);

                    Assert.NotNull(savedSession);
                    Assert.NotEqual(default(DateTime), savedSession.FinishedAt);
                    Assert.Single(savedSession.Files);
                    
                    var savedFile = savedSession.Files.First();
                    Assert.Equal(tempFilePath, savedFile.FilePath);
                    Assert.NotNull(savedFile.FileHash);
                    
                    var savedResult = savedFile.Result;
                    Assert.NotNull(savedResult);
                    // "hello integration test world" -> 28 chars, 4 words, 1 line, "integration" is longest word
                    Assert.Equal(28, savedResult.CharCount);
                    Assert.Equal(4, savedResult.WordCount);
                    Assert.Equal(1, savedResult.LineCount);
                    Assert.Equal("integration", savedResult.LongestWord);
                }
            }
            finally
            {
                // Clean up the temporary file
                if (File.Exists(tempFilePath))
                {
                    File.Delete(tempFilePath);
                }
            }
        }
    }
}
