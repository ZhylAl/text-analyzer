using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text.Json;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Infrastructure.Messaging;

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
            var factory = new ConnectionFactory { HostName = _settings.HostName };
            // Подключаемся к RabbitMQ (using гарантирует закрытие при остановке Воркера)
            using var connection = await factory.CreateConnectionAsync(stoppingToken);
            using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Убеждаемся, что очередь существует
            await channel.QueueDeclareAsync(_settings.QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

            _logger.LogInformation("Worker is waiting for messages from RabbitMQ...");

            // Создаем потребителя
            var consumer = new AsyncEventingBasicConsumer(channel);

            // Это событие стреляет каждый раз, когда в очереди появляется новый JSON
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = JsonSerializer.Deserialize<FileBatchAnalysisMessage>(body);

                if (message != null)
                {
                    _logger.LogInformation("Received batch for session {SessionId}", message.SessionId);

                    // ==============================================================
                    // TODO: ТВОЯ ЗАДАЧА ЗДЕСЬ! НАПИШИ ПРАВИЛЬНЫЙ ВЫЗОВ
                    // 1. Создай изолированный Scope через _serviceProvider
                    // 2. Достань ProcessBatchUseCase из этого Scope
                    // 3. Вызови метод ExecuteAsync у юзкейса (передай message и stoppingToken)
                    // ==============================================================

                    // Сообщаем кролику, что сообщение успешно обработано и его можно удалять из очереди! (Механизм надежности)
                    await channel.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false, cancellationToken: stoppingToken);
                }
            };

            // Запускаем бесконечное прослушивание очереди
            await channel.BasicConsumeAsync(queue: _settings.QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

            // Чтобы Воркер не завершился сразу, заставляем его поток ждать отмены (остановки приложения)
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}
