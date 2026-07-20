using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TextAnalyzer.Infrastructure.Data.Entities
{
    public class FileEntity
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(Session))]
        public Guid SessionId { get; set; }
        public SessionEntity Session { get; set; }

        public string FilePath { get; set; }

        public ResultEntity Result { get; set; }
    }
}
