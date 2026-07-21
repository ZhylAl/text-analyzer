namespace TextAnalyzer.IntegrationTests
{
    public class TempDirectoryHelper : IDisposable
    {
        public string FolderPath { get; }

        public TempDirectoryHelper()
        {
            FolderPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(FolderPath);

            var file1 = Path.Combine(FolderPath, "file1.txt");
            var file2 = Path.Combine(FolderPath, "file2.txt");
            File.WriteAllText(file1, @"Content for  
file1
");
            File.WriteAllText(file2, "Coooooooooooooooooooooooooontent for file2");

             
        }

        public void Dispose()
        {
            if (Directory.Exists(FolderPath))
            {
                Directory.Delete(FolderPath, true);
            }
        }
    }
}
