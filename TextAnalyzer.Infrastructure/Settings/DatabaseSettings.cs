using System.ComponentModel.DataAnnotations;

namespace TextAnalyzer.Infrastructure.Settings
{
    public class DatabaseSettings
    {
        [Required]
        public const string SectionName = "ConnectionStrings";
        [Required]
        public string DefaultConnection { get; set; } = string.Empty;
    }
}
