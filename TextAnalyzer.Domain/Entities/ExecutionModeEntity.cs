using System.ComponentModel.DataAnnotations;

namespace TextAnalyzer.Domain.Entities
{
    public class ExecutionModeEntity
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string ModeName { get; set; } = string.Empty;
        public ICollection<SessionEntity> Sessions { get; set; } = new List<SessionEntity>();
    }
}