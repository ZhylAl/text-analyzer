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
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppDomain.CurrentDomain.BaseDirectory,
            EnvironmentName = "Development"
        });

        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices(builder.Configuration);
        builder.Services.AddConsoleServices();

        var host = builder.Build();

        AnsiConsole.Write(new FigletText("Text Analyzer").Color(Color.Blue));

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            AnsiConsole.MarkupLine("\n[bold red]Cancelling operation...[/]");
            e.Cancel = true;
            cts.Cancel();
        };

        using var scope = host.Services.CreateScope();

        var runner = scope.ServiceProvider.GetRequiredService<ConsoleAppRunner>();
        await runner.RunAsync(cts.Token);
        Log.CloseAndFlush();
    }
}