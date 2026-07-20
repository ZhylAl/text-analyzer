using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Application.Models;
using TextAnalyzer.Infrastructure.Data.Entities;

namespace TextAnalyzer.Infrastructure.Data.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly TextAnalyzerDbContext _context;

        public SessionRepository(TextAnalyzerDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(SessionSaveDto sessionSaveDto)
        {
            var entity = new SessionEntity
            {
                Id = Guid.NewGuid(),
                StartedAt = sessionSaveDto.StartedAt,
                FinishedAt = sessionSaveDto.FinishedAt,
                ExecutionModeId = sessionSaveDto.ExecutionModeId,
                Files = new List<FileEntity>(),
                Results = new List<ResultEntity>()
            };

            foreach (var resultDto in sessionSaveDto.Results)
            {
                var fileEntity = new FileEntity
                {
                    FilePath = resultDto.FilePath 
                };

                var resultEntity = new ResultEntity
                {
                    File = fileEntity,
                    CharCount = resultDto.AnalysisResult.CharCount,
                    WordCount = resultDto.AnalysisResult.WordCount,
                    LineCount = resultDto.AnalysisResult.LineCount,
                    LongestWord = resultDto.AnalysisResult.LongestWord
                };
                
                entity.Files.Add(fileEntity);
                entity.Results.Add(resultEntity);
            }
            await _context.Sessions.AddAsync(entity);

            await _context.SaveChangesAsync();
        }
    }
}
