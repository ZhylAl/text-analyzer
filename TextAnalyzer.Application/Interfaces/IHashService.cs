namespace TextAnalyzer.Application.Interfaces
{
    public interface IHashService
    {
        string ComputeSha256Hash(string text);
    }
}