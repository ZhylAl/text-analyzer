using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TextAnalyzer.Infrastructure.Data.Entities
{
    public class ResultEntity
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(Session))]
        public Guid SessionId { get; set; }
        public SessionEntity Session { get; set; }

        [ForeignKey(nameof(File))]
        public Guid FileId { get; set; }
        public FileEntity File { get; set; }

        public int CharCount { get; set; }
        public int WordCount { get; set; }
        public int LineCount { get; set; }
        public string LongestWord { get; set; }
    }
}
