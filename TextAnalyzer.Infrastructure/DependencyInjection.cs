using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Infrastructure.Data;
using TextAnalyzer.Infrastructure.Export;
using TextAnalyzer.Infrastructure.Messaging;
using TextAnalyzer.Infrastructure.Reader;
using TextAnalyzer.Infrastructure.Settings;

namespace TextAnalyzer.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<RabbitMqSettings>()
                .BindConfiguration(RabbitMqSettings.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<DatabaseSettings>()
                .BindConfiguration(DatabaseSettings.SectionName)
                .ValidateDataAnnotations();

            services.AddOptions<ReaderSettings>()
                .BindConfiguration(ReaderSettings.SectionName)
                .ValidateDataAnnotations();

            services
                .AddSerilog((sp, loggerConfiguration) =>
                {
                    var dbSettings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseSettings>>().Value;

                    loggerConfiguration
                        .MinimumLevel.Information()
                        .WriteTo.Console()
                        .WriteTo.PostgreSQL(
                            connectionString: dbSettings.DefaultConnection,
                            tableName: "Logs",
                            needAutoCreateTable: true);
                })
                .AddDbContext<TextAnalyzerDbContext>((sp, options) =>
                {
                    var dbSettings = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<DatabaseSettings>>().Value;
                    options.UseNpgsql(dbSettings.DefaultConnection);
                })
                .AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<TextAnalyzerDbContext>())
                .AddSingleton<IFileReader, LocalFileReader>()
                .AddSingleton<IDirectoryReader, LocalDirectoryReader>()
                .AddSingleton<IFileAnalysisResultWriter, CsvFileAnalysisResultWriter>()
                .AddSingleton<IMessageProducer, RabbitMqProducer>();

            return services;
        }
    }
}