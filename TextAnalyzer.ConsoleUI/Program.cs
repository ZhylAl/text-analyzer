using System;
using Microsoft.Extensions.DependencyInjection;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.FileReader;
using TextAnalyzer.Domain.Services;
using TextAnalyzer.Infrastructure.FileReader;

namespace TextAnalyzer.ConsoleUI;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("--- Text Analyzer ---");
        Console.Write("Enter the path to the text file: ");
        string? filePath = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(filePath))
        {
            Console.WriteLine("Invalid path provided. Exiting.");
            return;
        }

        // DI Container
        var serviceProvider = new ServiceCollection()
            .AddSingleton<IFileReader, LocalFileReader>()
            .AddSingleton<ITextAnalyzerService, TextAnalyzerService>()
            .AddTransient<AnalyzeFileUseCase>()
            .BuildServiceProvider();

        var useCase = serviceProvider.GetRequiredService<AnalyzeFileUseCase>();

        try
        {
            var result = useCase.Execute(filePath);

            Console.WriteLine("\n--- Analysis Results ---");
            Console.WriteLine($"Characters:   {result.CharCount}");
            Console.WriteLine($"Words:        {result.WordCount}");
            Console.WriteLine($"Lines:        {result.LineCount}");
            Console.WriteLine($"Longest Word: {result.LongestWord}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }
}