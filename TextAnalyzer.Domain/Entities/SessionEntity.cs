using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TextAnalyzer.Domain.Entities
{
    public class SessionEntity
    {
        [Key]
        public Guid Id { get; set; }

        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        public DateTime FinishedAt { get; set; }

        [ForeignKey(nameof(ExecutionMode))]
        public int ExecutionModeId { get; set; }

        public ExecutionModeEntity ExecutionMode { get; set; }

        public ICollection<FileEntity> Files { get; set; } = new List<FileEntity>();
        public ICollection<ResultEntity> Results { get; set; } = new List<ResultEntity>();

    }
}

