using TextAnalyzer.Application.Models;

namespace TextAnalyzer.Application.Interfaces
{
    public interface ISessionRepository
    {
        Task AddAsync (SessionSaveDto sessionSaveDto);
    }
}
