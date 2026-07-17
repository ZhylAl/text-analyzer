using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Reader;
using TextAnalyzer.Application.Export;
using TextAnalyzer.Domain.Services;
using TextAnalyzer.Infrastructure.Export;
using TextAnalyzer.Infrastructure.Reader;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
namespace TextAnalyzer.ConsoleUI;

class Program
{
    static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Ok to get it like this? Without creating a dedicated class for settings? I think so, since it's just a single setting
        string[] allowedExtensions = configuration.GetSection("ReaderSettings:AllowedExtensions").Get<string[]>();

        // DI Container
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IFileReader, LocalFileReader>()
            .AddSingleton<IDirectoryReader>(sp => new LocalDirectoryReader(allowedExtensions))
            .AddSingleton<ITextAnalyzerService, TextAnalyzerService>()
            .AddSingleton<IFileAnalysisResultWriter, CsvFileAnalysisResultWriter>()
            .AddTransient<AnalyzeFileUseCase>()
            .AddTransient<AnalyzeFolderUseCase>()
            .BuildServiceProvider();

        AnsiConsole.Write(new FigletText("Text Analyzer").Color(Color.Blue));

        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("What would you like to do?")
                .PageSize(10)
                .AddChoices(new[] { "Analyze a single file", "Analyze a folder", "Exit" }));

        try
        {
            if (choice == "Analyze a single file")
            {
                AnalyzeSingleFile(serviceProvider);
            }
            else if (choice == "Analyze a folder")
            {
                AnalyzeFolder(serviceProvider);
            }
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
        }
    }

    static void AnalyzeSingleFile(ServiceProvider serviceProvider)
    {
        string filePath = AnsiConsole.Ask<string>("Enter the path to the text file:");

        var useCase = serviceProvider.GetRequiredService<AnalyzeFileUseCase>();
        
        TextAnalyzer.Domain.Models.TextAnalysisResult result = null;
        AnsiConsole.Status()
            .Start("Analyzing file...", ctx => 
            {
                result = useCase.Execute(filePath);
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

    static void AnalyzeFolder(ServiceProvider serviceProvider)
    {
        string folderPath = AnsiConsole.Ask<string>("Enter the path to the folder:");

        var useCase = serviceProvider.GetRequiredService<AnalyzeFolderUseCase>();
        
        string longestWord = string.Empty;
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .Start("Analyzing folder and generating CSV...", ctx => 
            {
                longestWord = useCase.Execute(folderPath);
            });

        AnsiConsole.MarkupLine("\n[bold green]Success![/] CSV report 'results.csv' has been generated in the target folder.");
        AnsiConsole.MarkupLine($"[blue]Overall Longest Word:[/] [bold]{longestWord}[/]");
    }
}