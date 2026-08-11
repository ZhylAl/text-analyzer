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

        public Worker(IServiceProvider serviceProvider, IOptions<RabbitMqSettings> options, ILogger<Worker> logger)
        {
            _serviceProvider = serviceProvider;
            _settings = options.Value;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var factory = new ConnectionFactory { Uri = new Uri(_settings.ConnectionString) };

            using var connection = await factory.CreateConnectionAsync(stoppingToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            await channel.QueueDeclareAsync(
                _settings.QueueName, 
                durable: true, 
                exclusive: false, 
                autoDelete: false, 
                arguments: null, 
                cancellationToken: stoppingToken);

            _logger.LogInformation("Worker is waiting for messages from RabbitMQ...");

            // Ensure fair dispatch: one message at a time per worker
            await channel.BasicQosAsync(0, 1, false, cancellationToken: stoppingToken);

            var consumer = new AsyncEventingBasicConsumer(channel);

            consumer.ReceivedAsync += async (model, ea) =>
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
            };

            await channel.BasicConsumeAsync(queue: _settings.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
