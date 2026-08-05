using Bogus;
using Spectre.Console;

namespace TextAnalyzer.FileGenerator
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                AnsiConsole.MarkupLine("\n[yellow]Cancelling generation... Please wait![/]");
                e.Cancel = true; 
                cts.Cancel();    
            };

            string filePath = @"C:\sample";
            int numberOfFiles = 5, fromMb = 0, upToMb = 50;

            while (true)

            {
                AnsiConsole.Clear();

                AnsiConsole.Write(new FigletText("File Generator").LeftJustified().Color(Color.Blue));

                var choice = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[blue]Configuration Menu (Select to edit)[/]")
                        .AddChoices(
                            $"1. Folder Path: [white]{filePath}[/]",
                            $"2. Files Count: [white]{numberOfFiles}[/]",
                            $"3. Min Size MB: [white]{fromMb}[/]",
                            $"4. Max Size MB: [white]{upToMb}[/]",
                            "[bold blue] START GENERATION[/]"
                        ));

                if (choice.StartsWith("1")) filePath = AnsiConsole.Ask<string>("Enter new Path: ").Trim('"');
                else if (choice.StartsWith("2")) numberOfFiles = AnsiConsole.Ask<int>("Enter new Count: ");
                else if (choice.StartsWith("3")) fromMb = AnsiConsole.Ask<int>("Enter new Min Size: ");
                else if (choice.StartsWith("4")) upToMb = AnsiConsole.Ask<int>("Enter new Max Size: ");
                else if (choice.StartsWith("[")) break;
            }

            if (!Directory.Exists(filePath))
                Directory.CreateDirectory(filePath);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Generating files...", async ctx =>
                {
                    var parallelOptions = new ParallelOptions
                    {
                        MaxDegreeOfParallelism = Environment.ProcessorCount,
                        CancellationToken = cts.Token
                    };

                    await Parallel.ForEachAsync(Enumerable.Range(0, numberOfFiles), parallelOptions, async (i, token) =>
                    {
                        // Обновляем текст лоадера
                        ctx.Status($"Generating file {i + 1} of {numberOfFiles}...");

                        // Вызываем генерацию
                        await GenerateRandomFile(fromMb, upToMb, filePath);
                    });
                });
            sw.Stop();

            AnsiConsole.MarkupLine($"\n[bold green]Success![/] All files have been successfully generated in [yellow]{sw.Elapsed.Minutes}m {sw.Elapsed.Seconds}s[/].");
        }

        static async Task GenerateRandomFile(int fromMb, int upToMb, string filePath)
        {
            var faker = new Faker("en");

            long targetSizeBytes = Random.Shared.Next(fromMb, upToMb + 1) * 1024L * 1024L;
            long currentBytes = 0;

            string fileName = $"file_{Guid.NewGuid()}.txt";
            string fullPath = Path.Combine(filePath, fileName);
            using (var writer = new StreamWriter(fullPath))
            {
                while (currentBytes < targetSizeBytes)
                {
                    string line = faker.Lorem.Paragraph();

                    await writer.WriteLineAsync(line);

                    currentBytes += line.Length + 2;
                }
            }
        }
    }
}
