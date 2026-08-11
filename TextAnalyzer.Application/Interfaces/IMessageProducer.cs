namespace TextAnalyzer.Application.Interfaces
{
    public interface IMessageProducer
    {
        Task PublishFileAnalysisRequestAsync(IEnumerable<string> filePaths, Guid sessionId, CancellationToken cancellationToken = default);
    }
}
