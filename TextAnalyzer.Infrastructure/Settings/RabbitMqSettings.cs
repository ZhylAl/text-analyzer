using System.ComponentModel.DataAnnotations;

namespace TextAnalyzer.Infrastructure.Settings;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";

    [Required(ErrorMessage = "CRITICAL: RabbitMQ ConnectionString is missing in appsettings.json!")]
    public string ConnectionString { get; set; } = "amqp://localhost:5672";

    [Required]
    public string QueueName { get; set; } = null!;

    public int MaxRetries { get; set; } = 10;
    public int RetryIntervalMs { get; set; } = 5000;
}