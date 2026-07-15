using TextAnalyzer.Domain.Models;

namespace TextAnalyzer.Domain.Services;

public class TextAnalyzerService : ITextAnalyzerService
{
    public TextAnalysisResult Analyze(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return new TextAnalysisResult(0, 0, 0, string.Empty);
        }

        int charCount = text.Length; // count all characters including space and punctuation and newlines
        //int charCount = text.Count(c => !char.IsWhiteSpace(c)); // count all characters excluding space and newlines and tabs
        //int charCount = text.Count(c => c != '\r' && c != '\n' && c != '\t'); // count all characters excluding newlines and tab


        int lineCount = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).Length; // with blank lines


        // getting all punctuation characters in the text and using them as delimiters for splitting words
        char[] punctuation = text.Where(char.IsPunctuation).Distinct().ToArray();
        char[] splitChars = punctuation.Concat(new[] { ' ', '\r', '\n', '\t' }).ToArray();

        var words = text.Split(splitChars, StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;

        string longestWord = words.MaxBy(w => w.Length) ?? string.Empty;

        return new TextAnalysisResult(charCount, wordCount, lineCount, longestWord);
    }
}