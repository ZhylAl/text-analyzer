using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Spectre.Console;
using System;
using System.Threading;
using System.Threading.Tasks;
using TextAnalyzer.Application;
using TextAnalyzer.Infrastructure;

namespace TextAnalyzer.ConsoleUI;

class Program
{
    static async Task Main(string[] args)
    {
        HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddConsoleServices();

        IHost host = builder.Build();

        AnsiConsole.Write(new FigletText("Text Analyzer").Color(Color.Blue));

        using CancellationTokenSource cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            AnsiConsole.MarkupLine("\n[bold red]Cancelling operation...[/]");
            e.Cancel = true;
            cts.Cancel();
        };

        using IServiceScope scope = host.Services.CreateScope();

        ConsoleAppRunner runner = scope.ServiceProvider.GetRequiredService<ConsoleAppRunner>();
        await runner.RunAsync(cts.Token);
        Log.CloseAndFlush();
    }
}