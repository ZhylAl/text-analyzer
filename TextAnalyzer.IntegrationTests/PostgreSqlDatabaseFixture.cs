using Testcontainers.PostgreSql;

namespace TextAnalyzer.IntegrationTests;

public class PostgreSqlDatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _dbContainer;

    public PostgreSqlDatabaseFixture()
    {
        _dbContainer = new PostgreSqlBuilder()
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .Build();
    }

    public string GetConnectionString() => _dbContainer.GetConnectionString();

    public Task InitializeAsync()
    {
        return _dbContainer.StartAsync();
    }

    public Task DisposeAsync()
    {
        return _dbContainer.DisposeAsync().AsTask();
    }
}