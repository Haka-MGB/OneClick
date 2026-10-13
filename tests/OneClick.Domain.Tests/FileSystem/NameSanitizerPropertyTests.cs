using FsCheck;
using FsCheck.Xunit;
using OneClick.Domain.FileSystem;
using Shouldly;
using Xunit;

namespace OneClick.Domain.Tests.FileSystem;

public class NameSanitizerPropertyTests
{
    [Property]
    public Property SanitizedNameShouldAlwaysBeValid(NonEmptyString input)
    {
        var result = NameSanitizer.Sanitize(input.Item);
        
        return result.IsSuccess.ToProperty()
            .And(() => result.IsSuccess ? NameSanitizer.Validate(result.Value).IsSuccess : true);
    }

    [Property] 
    public Property SanitizeShouldNeverReturnEmptyString(NonEmptyString input)
    {
        var result = NameSanitizer.Sanitize(input.Item);
        
        return (result.IsFailure || !string.IsNullOrWhiteSpace(result.Value)).ToProperty();
    }

    [Property]
    public Property SanitizeShouldRemoveInvalidCharacters(NonEmptyString input)
    {
        var result = NameSanitizer.Sanitize(input.Item);
        
        return result.IsSuccess.Implies(() =>
        {
            var invalidChars = new[] { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
            return !result.Value.Any(c => invalidChars.Contains(c) || char.IsControl(c));
        });
    }

    [Property]
    public Property ValidFileNameShouldPassValidation(ValidFileName fileName)
    {
        var result = NameSanitizer.Validate(fileName.Value);
        return result.IsSuccess.ToProperty();
    }

    [Property]
    public Property SanitizeIsIdempotent(NonEmptyString input)
    {
        var firstSanitize = NameSanitizer.Sanitize(input.Item);
        
        return firstSanitize.IsSuccess.Implies(() =>
        {
            var secondSanitize = NameSanitizer.Sanitize(firstSanitize.Value);
            return secondSanitize.IsSuccess && secondSanitize.Value == firstSanitize.Value;
        });
    }

    [Property]
    public Property LongNamesShouldBeTruncated(NonEmptyString input)
    {
        var veryLongName = new string('a', 300) + input.Item; // Create name over 255 chars
        var result = NameSanitizer.Sanitize(veryLongName);
        
        return result.IsSuccess.Implies(() => result.Value.Length <= 255);
    }

    // Custom generator for valid file names
    public class ValidFileName
    {
        public string Value { get; }
        
        public ValidFileName(string value)
        {
            Value = value;
        }
        
        public static Arbitrary<ValidFileName> Arbitrary()
        {
            var validChars = Gen.Elements("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_. ()[]{}".ToCharArray());
            var validName = Gen.NonEmptyListOf(validChars)
                .Select(chars => new string(chars.ToArray()))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Where(name => name.Length <= 255)
                .Where(name => !name.EndsWith('.') && !name.EndsWith(' '))
                .Where(name => !IsReservedName(name))
                .Select(name => new ValidFileName(name));
            
            return Arb.From(validName);
        }
        
        private static bool IsReservedName(string name)
        {
            var nameWithoutExt = Path.GetFileNameWithoutExtension(name);
            var reservedNames = new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" };
            return reservedNames.Contains(nameWithoutExt, StringComparer.OrdinalIgnoreCase);
        }
    }
    
    static NameSanitizerPropertyTests()
    {
        Arb.Register<ValidFileName>();
    }
}

public class NameSanitizerUnitTests
{
    [Theory]
    [InlineData("normal.txt", "normal.txt")]
    [InlineData("file<with>invalid:chars.txt", "file_with_invalid_chars.txt")]
    [InlineData("file with spaces.txt", "file with spaces.txt")]
    [InlineData("file.with.dots.txt", "file.with.dots.txt")]
    [InlineData("CON.txt", "_CON.txt")] // Reserved name
    [InlineData("trailing spaces   ", "trailing spaces")]
    [InlineData("trailing.dots...", "trailing.dots")]
    public void Sanitize_ShouldHandleKnownCases(string input, string expected)
    {
        // Act
        var result = NameSanitizer.Sanitize(input);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("...")]
    [InlineData("   ...   ")]
    public void Sanitize_ShouldFailForInvalidInputs(string input)
    {
        // Act
        var result = NameSanitizer.Sanitize(input);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Type.ShouldBe(OneClick.Domain.Common.ErrorType.InvalidName);
    }

    [Theory]
    [InlineData("normal.txt")]
    [InlineData("file with spaces.txt")]
    [InlineData("file.123.txt")]
    public void Validate_ShouldPassValidNames(string fileName)
    {
        // Act
        var result = NameSanitizer.Validate(fileName);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData("file<invalid>.txt")]
    [InlineData("CON")]
    [InlineData("trailing ")]
    [InlineData("trailing.")]
    public void Validate_ShouldFailInvalidNames(string fileName)
    {
        // Act
        var result = NameSanitizer.Validate(fileName);

        // Assert
        result.IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public void ValidatePath_ShouldCheckFileName()
    {
        // Arrange
        var invalidPath = @"C:\folder\CON.txt";

        // Act
        var result = NameSanitizer.ValidatePath(invalidPath);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("reserved device name");
    }

    [Fact]
    public void ValidatePath_ShouldHandleVeryLongPaths()
    {
        // Arrange - Create a path longer than 32000 characters
        var longPath = "C:\\" + new string('a', 32000);

        // Act
        var result = NameSanitizer.ValidatePath(longPath);

        // Assert
        result.IsSuccess.ShouldBeFalse();
        result.Error.Message.ShouldContain("exceeds system limits");
    }
}