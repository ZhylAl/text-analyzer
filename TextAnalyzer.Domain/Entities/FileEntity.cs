using System.ComponentModel.DataAnnotations;

namespace TextAnalyzer.Domain.Entities
{
    public class FileEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string? FileHash { get; set; } // string? because we already have rows in the db without hash
        public string FilePath { get; set; }

        public ResultEntity Result { get; set; }

        public ICollection<SessionEntity> Sessions { get; set; }
    }
}
