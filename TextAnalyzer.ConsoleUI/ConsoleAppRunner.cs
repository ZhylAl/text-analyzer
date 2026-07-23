using System;
using System.Linq;
using Spectre.Console;
using System.Threading;
using System.Threading.Tasks;
using TextAnalyzer.Application.Analysis;
using TextAnalyzer.Application.Models;

namespace TextAnalyzer.ConsoleUI
{
    public class ConsoleAppRunner
    {
        private readonly AnalyzeFileUseCase _analyzeFileUseCase;
        private readonly AnalyzeFolderUseCase _analyzeFolderUseCase;

        public ConsoleAppRunner(AnalyzeFileUseCase analyzeFileUseCase, AnalyzeFolderUseCase analyzeFolderUseCase)
        {
            _analyzeFileUseCase = analyzeFileUseCase;
            _analyzeFolderUseCase = analyzeFolderUseCase;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .PageSize(10)
                    .AddChoices(new[] { "Analyze a single file", "Analyze a folder", "Exit" }));

            try
            {
                if (choice == "Analyze a single file")
                {
                    await AnalyzeSingleFile(ct);
                }
                else if (choice == "Analyze a folder")
                {
                    await AnalyzeFolder(ct);
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

        private async Task AnalyzeSingleFile(CancellationToken ct)
        {
            string filePath = AnsiConsole.Ask<string>("Enter the path to the text file:").Trim('"');

            TextAnalyzer.Domain.Models.TextAnalysisResult result = null;
            await AnsiConsole.Status()
                .StartAsync("Analyzing file...", async ctx =>
                {
                    result = await _analyzeFileUseCase.Execute(filePath, ct);
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

        private async Task AnalyzeFolder(CancellationToken ct)
        {
            string folderPath = AnsiConsole.Ask<string>("Enter the path to the folder:").Trim('"');

            AnalyzeFolderResponse response = null;
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Analyzing folder and generating CSV...", async ctx =>
                {
                    response = await _analyzeFolderUseCase.Execute(folderPath, ct);
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
}
