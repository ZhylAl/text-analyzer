using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Entities;

namespace TextAnalyzer.Application.Analysis
{
    public class EnqueueAnalysisTasksUseCase
    {
        private readonly IApplicationDbContext _dbContext;
        private readonly IMessageProducer _messageProducer; 

        public EnqueueAnalysisTasksUseCase(IApplicationDbContext dbContext, IMessageProducer messageProducer)
        {
            _dbContext = dbContext;
            _messageProducer = messageProducer;
        }

        public async Task<Guid> ExecuteAsync(IEnumerable<string> filePaths, CancellationToken ct = default)
        {
            var session = new SessionEntity
            {
                Id = Guid.NewGuid(),
                StartedAt = DateTime.UtcNow,
                ExecutionModeId = (int)ExecutionMode.Folder
            };

            _dbContext.Sessions.Add(session);
            await _dbContext.SaveChangesAsync(ct);

            var chunks = filePaths.Chunk(500);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = Environment.ProcessorCount,
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(chunks, parallelOptions, async (chunk, ct) =>
            {
                await _messageProducer.PublishFileAnalysisRequestAsync(chunk, session.Id, ct);
            });
            
            return session.Id;
        }
    }
}
