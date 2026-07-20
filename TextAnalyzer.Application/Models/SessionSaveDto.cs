using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Application.Models
{
    public sealed record SessionSaveDto(
        DateTime StartedAt,
        DateTime FinishedAt,
        int ExecutionModeId,
        IReadOnlyCollection<FileAnalysisResult> Results);
}
