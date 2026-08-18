using TextAnalyzer.Application;
using TextAnalyzer.Infrastructure;


namespace TextAnalyzer.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddApplicationServices();
            builder.Services.AddInfrastructureServices(builder.Configuration);

            builder.Services.AddHostedService<Worker>();

            IHost host = builder.Build();
            host.Run();
        }
    }
}