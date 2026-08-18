using Microsoft.Extensions.DependencyInjection;

namespace TextAnalyzer.ConsoleUI
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddConsoleServices(this IServiceCollection services)
        {
            services.AddTransient<ConsoleAppRunner>();
            return services;
        }
    }
}