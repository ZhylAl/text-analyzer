using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TextAnalyzer.Domain.Entities
{
    public class ResultEntity
    {
        [Key]
        public Guid Id { get; set; }

        [ForeignKey(nameof(File))]
        public Guid FileId { get; set; }
        public FileEntity File { get; set; }

        public int CharCount { get; set; }
        public int WordCount { get; set; }
        public int LineCount { get; set; }
        public string LongestWord { get; set; }
    }
}
