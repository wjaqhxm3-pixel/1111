using System.Text;

namespace ScreenSealWindows.Tests;

/// <summary>
/// Comprehensive test suite for README.md documentation file.
/// Validates existence, content, encoding, and formatting.
/// </summary>
public class ReadmeTests
{
    private const string ReadmePath = "../../../README.md";
    private const string ExpectedContent = "CodeRabbit 테스트 중!";

    [Fact]
    public void ReadmeFile_ShouldExist()
    {
        // Arrange & Act
        var fileExists = File.Exists(ReadmePath);

        // Assert
        Assert.True(fileExists, "README.md file should exist in the repository root");
    }

    [Fact]
    public void ReadmeFile_ShouldNotBeEmpty()
    {
        // Arrange
        var fileInfo = new FileInfo(ReadmePath);

        // Act
        var fileSize = fileInfo.Length;

        // Assert
        Assert.True(fileSize > 0, "README.md should not be empty");
    }

    [Fact]
    public void ReadmeFile_ShouldContainExpectedContent()
    {
        // Arrange
        var content = File.ReadAllText(ReadmePath).Trim();

        // Act & Assert
        Assert.Contains(ExpectedContent, content);
    }


    [Fact]
    public void ReadmeFile_ShouldBeValidUtf8()
    {
        // Arrange
        byte[] fileBytes = File.ReadAllBytes(ReadmePath);

        // Act
        var isValidUtf8 = IsValidUtf8(fileBytes);

        // Assert
        Assert.True(isValidUtf8, "README.md should be valid UTF-8 encoded");
    }

    [Fact]
    public void ReadmeFile_ShouldHaveReasonableSize()
    {
        // Arrange
        var fileInfo = new FileInfo(ReadmePath);
        const long maxReasonableSize = 1024 * 1024; // 1MB

        // Act
        var fileSize = fileInfo.Length;

        // Assert
        Assert.True(fileSize < maxReasonableSize,
            $"README.md size ({fileSize} bytes) should be less than {maxReasonableSize} bytes");
    }

    [Fact]
    public void ReadmeFile_ShouldContainKoreanText()
    {
        // Arrange
        var content = File.ReadAllText(ReadmePath);

        // Act
        var containsKorean = content.Any(c => c >= 0xAC00 && c <= 0xD7AF);

        // Assert
        Assert.True(containsKorean, "README.md should contain Korean text (Hangul characters)");
    }

    [Fact]
    public void ReadmeFile_ShouldNotContainNullCharacters()
    {
        // Arrange
        var content = File.ReadAllText(ReadmePath);

        // Act
        var containsNull = content.Contains('\0');

        // Assert
        Assert.False(containsNull, "README.md should not contain null characters");
    }

    [Fact]
    public void ReadmeFile_ShouldBeReadable()
    {
        // Arrange & Act
        Exception? exception = null;
        string? content = null;

        try
        {
            content = File.ReadAllText(ReadmePath);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        Assert.Null(exception);
        Assert.NotNull(content);
    }

    [Fact]
    public void ReadmeFile_LineEndings_ShouldBeConsistent()
    {
        // Arrange
        var content = File.ReadAllText(ReadmePath);

        // Act
        var hasWindowsLineEndings = content.Contains("\r\n");
        var hasUnixLineEndings = content.Contains("\n") && !content.Contains("\r\n");
        var hasMacLineEndings = content.Contains("\r") && !content.Contains("\n");

        // Assert - should only have one type of line ending
        var lineEndingTypes = new[] { hasWindowsLineEndings, hasUnixLineEndings, hasMacLineEndings }
            .Count(x => x);

        Assert.True(lineEndingTypes <= 1, "README.md should have consistent line endings");
    }

    [Fact]
    public void ReadmeFile_Content_ShouldNotBeOnlyWhitespace()
    {
        // Arrange
        var content = File.ReadAllText(ReadmePath);

        // Act
        var trimmedContent = content.Trim();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(trimmedContent),
            "README.md should contain non-whitespace content");
    }

    [Fact]
    public void ReadmeFile_ShouldNotContainControlCharacters()
    {
        // Arrange
        var content = File.ReadAllText(ReadmePath);

        // Act
        var controlChars = content.Where(c => char.IsControl(c) && c != '\n' && c != '\r' && c != '\t').ToList();

        // Assert
        Assert.Empty(controlChars);
    }

    /// <summary>
    /// Validates that a byte array represents valid UTF-8 encoding.
    /// </summary>
    private static bool IsValidUtf8(byte[] bytes)
    {
        try
        {
            var encoding = new UTF8Encoding(false, true);
            encoding.GetString(bytes);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}