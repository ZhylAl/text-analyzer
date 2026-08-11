using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Infrastructure.Settings;

namespace TextAnalyzer.Infrastructure.Messaging;

public class RabbitMqProducer : IMessageProducer
{
    private IConnection? _connection;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);
    private readonly ConnectionFactory _factory;
    private readonly string _queueName;

    public RabbitMqProducer(IOptions<RabbitMqSettings> options)
    {
        var settings = options.Value;
        _queueName = settings.QueueName;
        _factory = new ConnectionFactory { Uri = new Uri(settings.ConnectionString) };
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is not null) return _connection;

        await _connectionLock.WaitAsync(ct); 
        try
        {
            _connection ??= await _factory.CreateConnectionAsync(ct);
            return _connection;
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task PublishFileAnalysisRequestAsync(IEnumerable<string> filePaths, Guid sessionId, CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: _queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var message = new FileBatchAnalysisMessage(sessionId, filePaths);

        var body = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message);

        var props = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: _queueName,
            mandatory: true,
            basicProperties: props,
            body: body,
            cancellationToken: cancellationToken);
    }
}