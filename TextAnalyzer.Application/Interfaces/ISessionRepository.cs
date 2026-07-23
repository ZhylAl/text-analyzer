using TextAnalyzer.Application.Models;
using TextAnalyzer.Domain.Entities;

namespace TextAnalyzer.Application.Interfaces
{
    public interface ISessionRepository
    {
        Task AddAsync (SessionEntity entity);
    }
}
