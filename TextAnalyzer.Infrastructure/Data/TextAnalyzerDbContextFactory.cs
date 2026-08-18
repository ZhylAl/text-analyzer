using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace TextAnalyzer.Infrastructure.Data;

public class TextAnalyzerDbContextFactory : IDesignTimeDbContextFactory<TextAnalyzerDbContext>
{
    public TextAnalyzerDbContext CreateDbContext(string[] args)
    {
        string basePath = Path.Combine(Directory.GetCurrentDirectory(), "../TextAnalyzer.ConsoleUI");

        IConfigurationRoot configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .Build();

        DbContextOptionsBuilder<TextAnalyzerDbContext> builder = new DbContextOptionsBuilder<TextAnalyzerDbContext>();
        string? connectionString = configuration.GetConnectionString("DefaultConnection");

        builder.UseNpgsql(connectionString);

        return new TextAnalyzerDbContext(builder.Options);
    }
}