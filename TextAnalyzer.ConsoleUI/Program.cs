using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Services;
using TextAnalyzer.Infrastructure.Data;
using TextAnalyzer.Infrastructure.Data.Repositories;
using TextAnalyzer.Infrastructure.Export;
using TextAnalyzer.Infrastructure.Reader;

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

        // Ok to get it like this? Without creating a dedicated class for settings? I think so, since it's just a single setting
        string[] allowedExtensions = configuration.GetSection("ReaderSettings:AllowedExtensions").Get<string[]>();

        // DI Container
        var serviceProvider = new ServiceCollection()
            .AddDbContext<TextAnalyzerDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")))
            .AddSingleton<IFileReader, LocalFileReader>()
            .AddSingleton<IDirectoryReader>(sp => new LocalDirectoryReader(allowedExtensions))
            .AddSingleton<ITextAnalyzerService, TextAnalyzerService>()
            .AddSingleton<IFileAnalysisResultWriter, CsvFileAnalysisResultWriter>()
            .AddScoped<ISessionRepository, SessionRepository>()
            .AddTransient<AnalyzeFileUseCase>()
            .AddTransient<AnalyzeFolderUseCase>()
            .BuildServiceProvider();

        AnsiConsole.Write(new FigletText("Text Analyzer").Color(Color.Blue));

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .PageSize(10)
                .AddChoices(new[] { "Analyze a single file", "Analyze a folder", "Exit" }));

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (sender, e) =>
        {
            AnsiConsole.MarkupLine("\n[bold red]Cancelling operation...[/]");
            e.Cancel = true;
            cts.Cancel();
        };

        try
        {
            if (choice == "Analyze a single file")
            {
                await AnalyzeSingleFile(serviceProvider, cts.Token); 
            }
            else if (choice == "Analyze a folder")
            {
                await AnalyzeFolder(serviceProvider, cts.Token); 
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.MarkupLine("\n[bold yellow]Analysis was cancelled by the user.[/]");
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }
    }

    static async Task AnalyzeSingleFile(ServiceProvider serviceProvider, CancellationToken ct)
    {
        string filePath = AnsiConsole.Ask<string>("Enter the path to the text file:").Trim('"');

        var useCase = serviceProvider.GetRequiredService<AnalyzeFileUseCase>();
        
        TextAnalyzer.Domain.Models.TextAnalysisResult result = null;
        await AnsiConsole.Status()
            .StartAsync("Analyzing file...", async ctx => 
            {
                result = await useCase.Execute(filePath, ct);
            });

        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Value");

        table.AddRow("Characters", result.CharCount.ToString());
        table.AddRow("Words", result.WordCount.ToString());
        table.AddRow("Lines", result.LineCount.ToString());
        table.AddRow("Longest Word", result.LongestWord);

        AnsiConsole.Write(
            new Panel(table)
                .Header("[blue]Analysis Results[/]")
                .BorderColor(Color.Blue));
    }

    static async Task AnalyzeFolder(ServiceProvider serviceProvider, CancellationToken ct)
    {
        string folderPath = AnsiConsole.Ask<string>("Enter the path to the folder:").Trim('"');

        var useCase = serviceProvider.GetRequiredService<AnalyzeFolderUseCase>();

        AnalyzeFolderResponse response = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync("Analyzing folder and generating CSV...", async ctx => 
            {
                response = await useCase.Execute(folderPath, ct);
            });

        AnsiConsole.MarkupLine("\n[bold green]Success![/] CSV report 'results.csv' has been generated in the target folder.");
        AnsiConsole.MarkupLine($"[blue]Overall Longest Word:[/] [bold]{response!.LongestWordOverall}[/]");

        if (response.Errors.Any())
        {
            foreach (var error in response.Errors)
            {
                AnsiConsole.MarkupLine($"[yellow]{error}[/]");
            }
        }
    }
}