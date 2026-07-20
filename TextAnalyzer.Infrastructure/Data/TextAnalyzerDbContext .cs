using Microsoft.EntityFrameworkCore;
using TextAnalyzer.Infrastructure.Data.Entities;

namespace TextAnalyzer.Infrastructure.Data;

public class TextAnalyzerDbContext : DbContext
{
    public TextAnalyzerDbContext(DbContextOptions<TextAnalyzerDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ExecutionModeEntity>().HasData(
            new ExecutionModeEntity { Id = (int)ExecutionMode.SingleFile, ModeName = "SingleFile" },
            new ExecutionModeEntity { Id = (int)ExecutionMode.Folder, ModeName = "Folder" }
        );
    }


    public DbSet<SessionEntity> Sessions { get; set; }
    public DbSet<FileEntity> Files { get; set; }
    public DbSet<ResultEntity> Results { get; set; }
    public DbSet<ExecutionModeEntity> ExecutionModes { get; set; }
}