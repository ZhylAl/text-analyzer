using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using TextAnalyzer.Infrastructure.Settings;
using TextAnalyzer.Worker;

namespace TextAnalyzer.Tests;

public class WorkerTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldThrowInvalidOperationException_WhenMaxRetriesReached()
    {
        // Arrange
        RabbitMqSettings settings = new RabbitMqSettings
        {
            MaxRetries = 3,
            RetryIntervalMs = 1,
            QueueName = "test-queue"
        };

        Mock<IOptions<RabbitMqSettings>> optionsMock = new Mock<IOptions<RabbitMqSettings>>();
        optionsMock.Setup(o => o.Value).Returns(settings);

        Mock<ILogger<Worker.Worker>> loggerMock = new Mock<ILogger<TextAnalyzer.Worker.Worker>>();
        Mock<IServiceProvider> serviceProviderMock = new Mock<IServiceProvider>();

        Mock<IConnectionFactory> connectionFactoryMock = new Mock<IConnectionFactory>();
        connectionFactoryMock.Setup(x => x.CreateConnectionAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new Exception("Network down"));

        TestableWorker worker = new TestableWorker(serviceProviderMock.Object, optionsMock.Object, loggerMock.Object, connectionFactoryMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await worker.RunExecuteAsyncForTest(CancellationToken.None));
    }
}

public class TestableWorker : TextAnalyzer.Worker.Worker
{
    public TestableWorker(IServiceProvider serviceProvider, IOptions<RabbitMqSettings> options, ILogger<TextAnalyzer.Worker.Worker> logger, IConnectionFactory connectionFactory)
        : base(serviceProvider, options, logger, connectionFactory)
    {
    }

    public Task RunExecuteAsyncForTest(CancellationToken token)
    {
        return base.ExecuteAsync(token);
    }
}
