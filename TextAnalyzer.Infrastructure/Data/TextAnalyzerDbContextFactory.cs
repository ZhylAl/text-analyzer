using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TextAnalyzer.Infrastructure.Data;

public class TextAnalyzerDbContextFactory : IDesignTimeDbContextFactory<TextAnalyzerDbContext>
{
    public TextAnalyzerDbContext CreateDbContext(string[] args)
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "../TextAnalyzer.ConsoleUI");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        var builder = new DbContextOptionsBuilder<TextAnalyzerDbContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        builder.UseNpgsql(connectionString);

        return new TextAnalyzerDbContext(builder.Options);
    }
}