using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Infrastructure.Settings;

namespace TextAnalyzer.Worker
{
    public class Worker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly RabbitMqSettings _settings;
        private readonly ILogger<Worker> _logger;
        private readonly IConnectionFactory _connectionFactory;

        public Worker(IServiceProvider serviceProvider, IOptions<RabbitMqSettings> options, ILogger<Worker> logger, IConnectionFactory connectionFactory)
        {
            _serviceProvider = serviceProvider;
            _settings = options.Value;
            _logger = logger;
            _connectionFactory = connectionFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int maxRetries = _settings.MaxRetries;
            int counter = 0;

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var connection = await _connectionFactory.CreateConnectionAsync(stoppingToken);
                    using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

                    await channel.QueueDeclareAsync(
                        _settings.QueueName,
                        durable: true,
                        exclusive: false,
                        autoDelete: false,
                        arguments: new Dictionary<string, object?>
                        {
                            { "x-dead-letter-exchange", "my-dlx" }
                        },
                        cancellationToken: stoppingToken);

                    await channel.ExchangeDeclareAsync("my-dlx", ExchangeType.Fanout, durable: true, cancellationToken: stoppingToken);
                    await channel.QueueDeclareAsync("my-dlq", durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
                    await channel.QueueBindAsync(queue: "my-dlq", exchange: "my-dlx", routingKey: "", cancellationToken: stoppingToken);

                    _logger.LogInformation("Worker is waiting for messages from RabbitMQ...");

                    // Ensure fair dispatch: one message at a time per worker
                    await channel.BasicQosAsync(0, 1, false, cancellationToken: stoppingToken);

                    var consumer = new AsyncEventingBasicConsumer(channel);

                    consumer.ReceivedAsync += async (model, ea) =>
                    {
                        try
                        {
                            var body = ea.Body.ToArray();
                            var message = JsonSerializer.Deserialize<FileBatchAnalysisMessage>(body);

                            if (message != null)
                            {
                                _logger.LogInformation("Received batch for session {SessionId}. First file: {FirstFile}", message.SessionId, message.FilePaths.FirstOrDefault());

                                using var scope = _serviceProvider.CreateScope();
                                var useCase = scope.ServiceProvider.GetRequiredService<ProcessBatchUseCase>();
                                await useCase.ExecuteAsync(message, stoppingToken);

                                await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                            }
                            else
                            {
                                _logger.LogError("Failed to deserialize message");

                                await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to process message");
                            await channel.BasicNackAsync(deliveryTag: ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: stoppingToken);
                        }
                    };

                    await channel.BasicConsumeAsync(queue: _settings.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

                    counter = 0;
                    await Task.Delay(Timeout.Infinite, stoppingToken);
                }
                catch (Exception ex)
                {
                    counter++;
                    if (counter == maxRetries)
                    {
                        _logger.LogError(ex, "Error: Max retries ({Retries}) reached. Stopping worker.", maxRetries);
                        throw new InvalidOperationException($"RabbitMQ connection failed after {maxRetries} retries", ex);
                    }

                    _logger.LogError(ex, "Error: Number of attempts: {Counter}", counter);
                    await Task.Delay(_settings.RetryIntervalMs);
                }
            }
        }
    }
}
