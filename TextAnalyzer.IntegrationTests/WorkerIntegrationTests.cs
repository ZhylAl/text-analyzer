using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TextAnalyzer.Infrastructure.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace TextAnalyzer.IntegrationTests;

public class WorkerIntegrationTests : IClassFixture<RabbitMqFixture>
{
    private readonly RabbitMqFixture _fixture;

    public WorkerIntegrationTests(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Worker_ShouldCreateQueuesAndExchanges_InRealRabbitMq()
    {
        // Arrange
        var services = new ServiceCollection();

        services.AddLogging();

        var settings = new RabbitMqSettings { MaxRetries = 3, RetryIntervalMs = 100, QueueName = "integration-queue" };
        services.AddSingleton(Options.Create(settings));

        var connectionFactory = new ConnectionFactory { Uri = new Uri(_fixture.GetConnectionString()) };
        services.AddSingleton<IConnectionFactory>(connectionFactory);

        services.AddTransient<TextAnalyzer.Worker.Worker>();

        var provider = services.BuildServiceProvider();
        var worker = provider.GetRequiredService<TextAnalyzer.Worker.Worker>();

        // Act
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        await worker.StartAsync(cts.Token);
        await Task.Delay(500);

        // Assert
        using var connection = await connectionFactory.CreateConnectionAsync();
        using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclarePassiveAsync("integration-queue");
        await channel.QueueDeclarePassiveAsync("my-dlq");

        await worker.StopAsync(CancellationToken.None);
    }
}
