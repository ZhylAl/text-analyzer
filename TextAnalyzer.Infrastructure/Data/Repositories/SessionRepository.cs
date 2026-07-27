using Microsoft.EntityFrameworkCore;
using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Infrastructure.Data.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly IDbContextFactory<TextAnalyzerDbContext> _contextFactory;

        public SessionRepository(IDbContextFactory<TextAnalyzerDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        public async Task AddAsync(SessionEntity entity)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();
            await _context.Sessions.AddAsync(entity);

            await _context.SaveChangesAsync();
        }

        public async Task<TextAnalysisResult?> GetCachedResultAsync(string fileHash)
        {
            await using var _context = await _contextFactory.CreateDbContextAsync();
            var fileEntity = await _context.Files
                .Include(f => f.Result) 
                .FirstOrDefaultAsync(f => f.FileHash == fileHash);

            if (fileEntity?.Result == null)
                return null;

            return new TextAnalysisResult(
                fileEntity.Result.CharCount,
                fileEntity.Result.WordCount,
                fileEntity.Result.LineCount,
                fileEntity.Result.LongestWord
            );
        }

    }
}
