using System;
using System.IO;
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
        private readonly EnqueueAnalysisTasksUseCase _enqueueAnalysisTasksUseCase;

        public ConsoleAppRunner(AnalyzeFileUseCase analyzeFileUseCase, AnalyzeFolderUseCase analyzeFolderUseCase, EnqueueAnalysisTasksUseCase enqueueAnalysisTasksUseCase)
        {
            _analyzeFileUseCase = analyzeFileUseCase;
            _analyzeFolderUseCase = analyzeFolderUseCase;
            _enqueueAnalysisTasksUseCase = enqueueAnalysisTasksUseCase;
        }

        public async Task RunAsync(CancellationToken ct)
        {
            var choice = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("What would you like to do?")
                    .PageSize(10)
                    .AddChoices(new[] { "Analyze a single file", "Analyze a folder", "Analyze folder (Distributed / RabbitMQ)","Exit" }));

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
                else if (choice == "Analyze folder (Distributed / RabbitMQ)")
                {
                    await AnalyzeFolderDistributed(ct);
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

        private async Task AnalyzeFolderDistributed(CancellationToken ct)
        {
            string folderPath = AnsiConsole.Ask<string>("Enter the path to the folder:").Trim('"');

            var files = Directory.GetFiles(folderPath, "*.txt", SearchOption.AllDirectories)
                .Where(f => !f.Equals("results.csv", StringComparison.OrdinalIgnoreCase))
                .ToList();

            Guid sessionId = Guid.Empty;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Analyzing folder and generating CSV...", async ctx =>
                {
                    sessionId = await _enqueueAnalysisTasksUseCase.ExecuteAsync(files, ct);
                });

            AnsiConsole.MarkupLine($"\n[bold green]Success![/] Successfully queued [yellow]{files.Count}[/] files for distributed analysis.");
            AnsiConsole.MarkupLine($"[blue]Session ID:[/] [bold]{sessionId}[/]");
            AnsiConsole.MarkupLine("[grey]Workers will process these files in the background. The results will be saved to the database.[/]");
        }
    }
}
