namespace TextAnalyzer.Application.Models
{
    public record FileBatchAnalysisMessage(Guid SessionId, IEnumerable<string> FilePaths);
}
