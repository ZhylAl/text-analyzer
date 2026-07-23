using TextAnalyzer.Application.Interfaces;
using TextAnalyzer.Domain.Entities;

namespace TextAnalyzer.Infrastructure.Data.Repositories
{
    public class SessionRepository : ISessionRepository
    {
        private readonly TextAnalyzerDbContext _context;

        public SessionRepository(TextAnalyzerDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(SessionEntity entity)
        {
            await _context.Sessions.AddAsync(entity);

            await _context.SaveChangesAsync();
        }
    }
}
