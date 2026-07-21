namespace TextAnalyzer.IntegrationTests
{
    public class TempFileHelper : IDisposable
    {
        public string FilePath { get; }

        public TempFileHelper(string content)
        {
            FilePath = Path.GetTempFileName();
            File.WriteAllText(FilePath, content);
        }

        public void Dispose()
        {
            if (File.Exists(FilePath))
            {
                File.Delete(FilePath);
            }
        }
    }
}
