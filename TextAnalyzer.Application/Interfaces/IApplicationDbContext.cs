using Microsoft.EntityFrameworkCore;
using TextAnalyzer.Domain.Entities;

namespace TextAnalyzer.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<SessionEntity> Sessions { get; }
    DbSet<FileEntity> Files { get; }
    DbSet<ResultEntity> Results { get; }
    DbSet<ExecutionModeEntity> ExecutionModes { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}