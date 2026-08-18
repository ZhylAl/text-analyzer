using System.ComponentModel.DataAnnotations;

namespace TextAnalyzer.Infrastructure.Settings
{
    public class ReaderSettings
    {
        [Required]
        public const string SectionName = "ReaderSettings";
        [Required]
        public string[] AllowedExtensions { get; set; } = Array.Empty<string>();
    }
}
