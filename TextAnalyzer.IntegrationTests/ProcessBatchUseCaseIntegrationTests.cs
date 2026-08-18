using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Application.Services;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;
using TextAnalyzer.Infrastructure.Data;
using TextAnalyzer.Infrastructure.Reader;

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
            DbContextOptions<TextAnalyzerDbContext> options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
                .UseNpgsql(_dbFixture.GetConnectionString())
                .Options;

            using (TextAnalyzerDbContext setupContext = new TextAnalyzerDbContext(options))
            {
                await setupContext.Database.EnsureCreatedAsync(); 
                
                // Clean up
                setupContext.Sessions.RemoveRange(setupContext.Sessions);
                setupContext.Files.RemoveRange(setupContext.Files);
                setupContext.Results.RemoveRange(setupContext.Results);
                await setupContext.SaveChangesAsync();
            }

            Guid sessionId = Guid.NewGuid();

            using (TextAnalyzerDbContext arrangeContext = new TextAnalyzerDbContext(options))
            {
                SessionEntity session = new SessionEntity { Id = sessionId, ExecutionModeId = 1 }; 
                arrangeContext.Sessions.Add(session);
                await arrangeContext.SaveChangesAsync();
            }

            TextAnalyzerService analyzerService = new TextAnalyzer.Application.Services.TextAnalyzerService();
            LocalFileReader fileReader = new TextAnalyzer.Infrastructure.Reader.LocalFileReader();
            HashService hashService = new TextAnalyzer.Application.Services.HashService();
            NullLogger<ProcessBatchUseCase> logger = NullLogger<ProcessBatchUseCase>.Instance;

            // Create a real temporary file
            string tempFilePath = Path.GetTempFileName();
            string testText = "hello integration test world";
            await File.WriteAllTextAsync(tempFilePath, testText);

            try
            {
                using (TextAnalyzerDbContext actContext = new TextAnalyzerDbContext(options))
                {
                    ProcessBatchUseCase useCase = new ProcessBatchUseCase(
                        actContext,
                        analyzerService,
                        fileReader,
                        hashService,
                        logger
                    );

                    FileBatchAnalysisMessage message = new FileBatchAnalysisMessage(sessionId, new[] { tempFilePath });

                    // Act
                    await useCase.ExecuteAsync(message, CancellationToken.None);
                }

                using (TextAnalyzerDbContext assertContext = new TextAnalyzerDbContext(options))
                {
                    // Assert
                    SessionEntity? savedSession = await assertContext.Sessions
                        .Include(s => s.Files)
                        .ThenInclude(f => f.Result)
                        .SingleOrDefaultAsync(s => s.Id == sessionId);

                    Assert.NotNull(savedSession);
                    Assert.NotEqual(default(DateTime), savedSession.FinishedAt);
                    Assert.Single(savedSession.Files);
                    
                    FileEntity savedFile = savedSession.Files.First();
                    Assert.Equal(tempFilePath, savedFile.FilePath);
                    Assert.NotNull(savedFile.FileHash);
                    
                    ResultEntity savedResult = savedFile.Result;
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
