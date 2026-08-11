using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Spectre.Console;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TextAnalyzer.ConsoleUI;

class Program
{
    static async Task Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
            .Build();

        configuration.ConfigureSerilog();

        var serviceProvider = new ServiceCollection()
            .AddApplicationServices(configuration)
            .BuildServiceProvider();

        AnsiConsole.Write(new FigletText("Text Analyzer").Color(Color.Blue));

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            AnsiConsole.MarkupLine("\n[bold red]Cancelling operation...[/]");
            e.Cancel = true;
            cts.Cancel();
        };

        var runner = serviceProvider.GetRequiredService<ConsoleAppRunner>();
        await runner.RunAsync(cts.Token);
        Log.CloseAndFlush();
    }
}