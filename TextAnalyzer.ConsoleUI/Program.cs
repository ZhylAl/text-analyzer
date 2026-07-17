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

        Console.WriteLine("--- Text Analyzer ---");
        Console.WriteLine("1. Analyze a single file");
        Console.WriteLine("2. Analyze a folder");
        Console.Write("Select an option (1 or 2): ");
        
        string? choice = Console.ReadLine();

        try
        {
            if (choice == "1")
            {
                AnalyzeSingleFile(serviceProvider);
            }
            else if (choice == "2")
            {
                AnalyzeFolder(serviceProvider);
            }
            else
            {
                Console.WriteLine("Invalid option. Exiting.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    static void AnalyzeSingleFile(ServiceProvider serviceProvider)
    {
        Console.Write("Enter the path to the text file: ");
        string? filePath = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.WriteLine("Invalid path provided. Exiting.");
            return;
        }

        var useCase = serviceProvider.GetRequiredService<AnalyzeFileUseCase>();
        var result = useCase.Execute(filePath);

        Console.WriteLine("\n--- Analysis Results ---");
        Console.WriteLine($"Characters:   {result.CharCount}");
        Console.WriteLine($"Words:        {result.WordCount}");
        Console.WriteLine($"Lines:        {result.LineCount}");
        Console.WriteLine($"Longest Word: {result.LongestWord}");
    }

    static void AnalyzeFolder(ServiceProvider serviceProvider)
    {
        Console.Write("Enter the path to the folder: ");
        string? folderPath = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            Console.WriteLine("Invalid path provided. Exiting.");
            return;
        }

        var useCase = serviceProvider.GetRequiredService<AnalyzeFolderUseCase>();
        string longestWord = useCase.Execute(folderPath);

        Console.WriteLine("\n--- Analysis Results ---");
        Console.WriteLine("CSV report 'results.csv' has been generated in the target folder.");
        Console.WriteLine($"Overall Longest Word: {longestWord}");
    }
}