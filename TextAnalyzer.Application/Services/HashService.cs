using System.Security.Cryptography;
using System.Text;
using TextAnalyzer.Application.Interfaces;

namespace TextAnalyzer.Application.Services
{
    public class HashService : IHashService
    {
        public string ComputeSha256Hash(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            using var sha256 = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            byte[] hashBytes = sha256.ComputeHash(bytes);

            return Convert.ToHexString(hashBytes);
        }
    }
}