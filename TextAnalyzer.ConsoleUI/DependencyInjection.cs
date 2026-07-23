using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Services;
using TextAnalyzer.Infrastructure.Data;
using TextAnalyzer.Infrastructure.Data.Repositories;
using TextAnalyzer.Infrastructure.Export;
using TextAnalyzer.Infrastructure.Reader;

namespace TextAnalyzer.ConsoleUI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
        {
            string[] allowedExtensions = configuration.GetSection("ReaderSettings:AllowedExtensions").Get<string[]>();
            string connectionString = configuration.GetConnectionString("DefaultConnection");

            services
                .AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true))
                .AddDbContext<TextAnalyzerDbContext>(options => options.UseNpgsql(connectionString))
                .AddSingleton<IFileReader, LocalFileReader>()
                .AddSingleton<IDirectoryReader>(sp => new LocalDirectoryReader(allowedExtensions))
                .AddSingleton<ITextAnalyzerService, TextAnalyzerService>()
                .AddSingleton<IFileAnalysisResultWriter, CsvFileAnalysisResultWriter>()
                .AddScoped<ISessionRepository, SessionRepository>()
                .AddTransient<ConsoleAppRunner>()
                .AddTransient<AnalyzeFileUseCase>()
                .AddTransient<AnalyzeFolderUseCase>();

            return services;
        }

        public static void ConfigureSerilog(this IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("DefaultConnection");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.PostgreSQL(
                    connectionString,
                    tableName: "Logs",
                    needAutoCreateTable: true)
                .CreateLogger();
        }
    }
}
