using TextAnalyzer.Domain.Services;

namespace TextAnalyzer.Tests;

public class TextAnalyzerServiceTests
{
    private readonly TextAnalyzerService _analyzerService; 

    public TextAnalyzerServiceTests()
    {
        _analyzerService = new TextAnalyzerService();
    }

    [Fact]
    public void Analyze_WithNormalSentence_ReturnsCorrectCounts()
    {
        // Arrange
        string text = "Hello, world!";

        // Act
        var result = _analyzerService.Analyze(text);

        // Assert
        Assert.Equal(13, result.CharCount);
        Assert.Equal(2, result.WordCount);
        Assert.Equal(1, result.LineCount);
        Assert.Equal("Hello", result.LongestWord);
    }

    [Fact]
    public void Analyze_WithEmptyString_ReturnsZeros()
    {
        // Arrange
        string text = "";

        // Act
        var result = _analyzerService.Analyze(text);

        // Assert
        Assert.Equal(0, result.CharCount);
        Assert.Equal(0, result.WordCount);
        Assert.Equal(0, result.LineCount);
        Assert.Equal(string.Empty, result.LongestWord);
    }

    [Fact]
    public void Analyze_WithNullString_ReturnsZeros()
    {
        // Arrange
        string text = null;

        // Act
        var result = _analyzerService.Analyze(text);

        // Assert
        Assert.Equal(0, result.CharCount);
        Assert.Equal(0, result.WordCount);
        Assert.Equal(0, result.LineCount);
        Assert.Equal(string.Empty, result.LongestWord);
    }

    [Fact]
    public void Analyze_WithOnlyPunctuation_CountsCharactersButZeroWords()
    {
        // Arrange
        string text = "!?,.:;";

        // Act
        var result = _analyzerService.Analyze(text);

        // Assert
        Assert.Equal(6, result.CharCount);
        Assert.Equal(0, result.WordCount);
        Assert.Equal(1, result.LineCount);
        Assert.Equal(string.Empty, result.LongestWord); // No actual words
    }

    [Fact]
    public void Analyze_WithDoubleSpaces_DoesNotCountEmptyWords()
    {
        // Arrange
        string text = "One  two   three"; // Lots of spaces between words

        // Act
        var result = _analyzerService.Analyze(text);

        // Assert
        Assert.Equal(16, result.CharCount);
        Assert.Equal(3, result.WordCount);
        Assert.Equal(1, result.LineCount);
        Assert.Equal("three", result.LongestWord);

    }   

    [Fact]
    public void Analyze_WithMultiLineString_ReturnsCorrectCounts()
    {
        // Arrange
        string text = @"One  two   
three";

        // Act
        var result = _analyzerService.Analyze(text);

        // Assert
        Assert.Equal(18, result.CharCount);
        Assert.Equal(3, result.WordCount); 
        Assert.Equal(2, result.LineCount);
        Assert.Equal("three", result.LongestWord);

    }

    [Fact]
    public void Analyze_WithLongMultiLineString_ReturnsCorrectCounts()
    {
        // Arrange
        string text = @"This is a very long string.
It spans across multiple lines to test our line counting logic!
Let's add some tricky punctuation: (parentheses), ""quotes"", and hyphens-too.

Here is a blank line above. What about an incredibly long word like Supercalifragilisticexpialidocious?
Let's see if the analyzer handles it.";

        // Act
        var result = _analyzerService.Analyze(text);

        // Assert
        Assert.Equal(316, result.CharCount);
        Assert.Equal(50, result.WordCount);
        Assert.Equal(6, result.LineCount);
        Assert.Equal("Supercalifragilisticexpialidocious", result.LongestWord);
    }
}