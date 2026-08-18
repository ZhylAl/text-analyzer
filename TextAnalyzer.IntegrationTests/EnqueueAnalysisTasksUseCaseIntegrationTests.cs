using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Infrastructure.Data;
using TextAnalyzer.Infrastructure.Messaging;
using TextAnalyzer.Infrastructure.Settings;

namespace TextAnalyzer.IntegrationTests
{
    public class EnqueueAnalysisTasksUseCaseIntegrationTests : 
        IClassFixture<PostgreSqlDatabaseFixture>, 
        IClassFixture<RabbitMqFixture>
    {
        private readonly PostgreSqlDatabaseFixture _dbFixture;
        private readonly RabbitMqFixture _rabbitFixture;

        public EnqueueAnalysisTasksUseCaseIntegrationTests(
            PostgreSqlDatabaseFixture dbFixture, 
            RabbitMqFixture rabbitFixture)
        {
            _dbFixture = dbFixture;
            _rabbitFixture = rabbitFixture;
        }

        [Fact]
        public async Task ExecuteAsync_ShouldCreateSessionInRealDb_AndPublishMessagesToRealRabbitMq()
        {
            DbContextOptions<TextAnalyzerDbContext> options = new DbContextOptionsBuilder<TextAnalyzerDbContext>()
                .UseNpgsql(_dbFixture.GetConnectionString())
                .Options;

            using TextAnalyzerDbContext dbContext = new TextAnalyzerDbContext(options);
            await dbContext.Database.EnsureCreatedAsync(); 

            dbContext.Sessions.RemoveRange(dbContext.Sessions);
            await dbContext.SaveChangesAsync();

            IOptions<RabbitMqSettings> rabbitSettings = Options.Create(new RabbitMqSettings 
            { 
                ConnectionString = _rabbitFixture.GetConnectionString(),
                QueueName = "test_analysis_queue" 
            });

            RabbitMqProducer messageProducer = new RabbitMqProducer(rabbitSettings);
            EnqueueAnalysisTasksUseCase useCase = new EnqueueAnalysisTasksUseCase(dbContext, messageProducer);

            List<string> filePaths = Enumerable.Range(1, 1200).Select(i => $"file{i}.txt").ToList();

            // Act
            Guid sessionId = await useCase.ExecuteAsync(filePaths, CancellationToken.None);

            // Assert
            SessionEntity? savedSession = await dbContext.Sessions.SingleOrDefaultAsync(s => s.Id == sessionId);
            Assert.NotNull(savedSession);
            Assert.Equal((int)ExecutionMode.Folder, savedSession.ExecutionModeId);

            ConnectionFactory factory = new ConnectionFactory { Uri = new Uri(_rabbitFixture.GetConnectionString()) };
            using IConnection connection = await factory.CreateConnectionAsync();
            using IChannel channel = await connection.CreateChannelAsync();
            QueueDeclareOk queueInfo = await channel.QueueDeclarePassiveAsync("test_analysis_queue");
            Assert.Equal(3u, queueInfo.MessageCount);
        }
    }
}
