using System.ComponentModel.DataAnnotations;

namespace TextAnalyzer.Infrastructure.Messaging;

public class RabbitMqSettings
{
    public const string SectionName = "RabbitMQ";

    [Required(ErrorMessage = "CRITICAL: RabbitMQ HostName is missing in appsettings.json!")]
    public string HostName { get; set; } = null!;

    [Required]
    public string QueueName { get; set; } = null!;
}