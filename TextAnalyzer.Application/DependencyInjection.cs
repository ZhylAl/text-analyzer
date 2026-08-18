using Microsoft.Extensions.DependencyInjection;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Services;

namespace TextAnalyzer.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplicationServices(this IServiceCollection services)
        {
            services
                .AddSingleton<ITextAnalyzerService, TextAnalyzerService>()
                .AddSingleton<IHashService, HashService>()
                .AddTransient<AnalyzeFileUseCase>()
                .AddTransient<AnalyzeFolderUseCase>()
                .AddTransient<EnqueueAnalysisTasksUseCase>()
                .AddScoped<ProcessBatchUseCase>();

            return services;
        }
    }
}