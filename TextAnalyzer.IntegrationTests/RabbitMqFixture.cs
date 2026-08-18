using Testcontainers.RabbitMq;

namespace TextAnalyzer.IntegrationTests
{
    public class RabbitMqFixture : IAsyncLifetime
    {
        private readonly RabbitMqContainer _rabbitMqContainer;

        public RabbitMqFixture()
        {
            _rabbitMqContainer = new RabbitMqBuilder()
                .WithImage("rabbitmq:3-management")
                .WithUsername("test_user")
                .WithPassword("test_password")
                .Build();
        }

        public string GetConnectionString() => _rabbitMqContainer.GetConnectionString();

        public async Task InitializeAsync()
        {
            await _rabbitMqContainer.StartAsync();
        }

        public async Task DisposeAsync()
        {
            await _rabbitMqContainer.StopAsync();
        }
    }
}