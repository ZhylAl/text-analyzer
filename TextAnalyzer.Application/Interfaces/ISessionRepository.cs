using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;
using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Interfaces
{
    public interface ISessionRepository
    {
        Task AddAsync (SessionEntity entity);
        Task<TextAnalysisResult?> GetCachedResultAsync(string fileHash);

    }
}
