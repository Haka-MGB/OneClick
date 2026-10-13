using System.IO;
using System.Text;
using OneClick.Domain.Common;

namespace OneClick.Domain.FileSystem;

/// <summary>
/// Sanitizes and validates file names according to Windows file system rules.
/// Implements FR-CORE-14: Handle invalid characters, reserved names, etc.
/// </summary>
public static class NameSanitizer
{
    private static readonly char[] InvalidChars = { '<', '>', ':', '"', '/', '\\', '|', '?', '*' };
    private static readonly char[] ControlChars = Enumerable.Range(0, 32).Select(i => (char)i).ToArray();
    
    private static readonly string[] ReservedNames = 
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    /// <summary>
    /// Sanitizes a file name by removing invalid characters and patterns.
    /// </summary>
    /// <param name="fileName">The file name to sanitize</param>
    /// <param name="replacement">Character to use as replacement for invalid characters</param>
    /// <returns>Sanitized file name, or error if the name cannot be made valid</returns>
    public static Result<string> Sanitize(string fileName, char replacement = '_')
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result<string>.Failure(Error.InvalidName(fileName ?? "", "Name cannot be empty"));

        var sanitized = new StringBuilder(fileName.Length);

        // Replace invalid characters
        foreach (var c in fileName)
        {
            if (InvalidChars.Contains(c) || ControlChars.Contains(c))
            {
                sanitized.Append(replacement);
            }
            else
            {
                sanitized.Append(c);
            }
        }

        // Trim trailing spaces and dots (Windows doesn't allow these)
        var result = sanitized.ToString().TrimEnd(' ', '.');

        // Check if empty after sanitization
        if (string.IsNullOrEmpty(result))
            return Result<string>.Failure(Error.InvalidName(fileName, "Results in empty name after removing invalid characters"));

        // Check for reserved names
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(result);
        if (IsReservedName(nameWithoutExtension))
        {
            result = $"{replacement}{result}"; // Prefix with replacement character
        }

        // Check length limits
        if (result.Length > 255)
        {
            var extension = Path.GetExtension(result);
            var maxNameLength = 255 - extension.Length;
            if (maxNameLength <= 0)
                return Result<string>.Failure(Error.InvalidName(fileName, "Name too long even after truncation"));

            result = string.Concat(result.AsSpan(0, maxNameLength), extension);
        }

        return Result<string>.Success(result);
    }

    /// <summary>
    /// Validates a file name without modifying it.
    /// </summary>
    /// <param name="fileName">The file name to validate</param>
    /// <returns>Success if valid, otherwise an error describing the issue</returns>
    public static Result<string> Validate(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return Result<string>.Failure(Error.InvalidName(fileName ?? "", "Name cannot be empty"));

        // Check for invalid characters
        var invalidChar = fileName.FirstOrDefault(c => InvalidChars.Contains(c) || ControlChars.Contains(c));
        if (invalidChar != default)
            return Result<string>.Failure(Error.InvalidName(fileName, $"Contains invalid character '{invalidChar}'"));

        // Check for trailing spaces or dots
        if (fileName.EndsWith(' ') || fileName.EndsWith('.'))
            return Result<string>.Failure(Error.InvalidName(fileName, "Cannot end with space or dot"));

        // Check for reserved names
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        if (IsReservedName(nameWithoutExtension))
            return Result<string>.Failure(Error.InvalidName(fileName, $"'{nameWithoutExtension}' is a reserved device name"));

        // Check length
        if (fileName.Length > 255)
            return Result<string>.Failure(Error.InvalidName(fileName, "Name exceeds 255 characters"));

        return Result<string>.Success(fileName);
    }

    /// <summary>
    /// Validates a full file path for length limits.
    /// </summary>
    /// <param name="fullPath">The complete file path</param>
    /// <returns>Success if valid, otherwise an error</returns>
    public static Result<string> ValidatePath(string fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return Result<string>.Failure(Error.InvalidName(fullPath ?? "", "Path cannot be empty"));

        // Windows has a path limit, but we can use long path syntax (\\?\) internally
        // For user display, we'll warn at 260 characters
        if (fullPath.Length > 32000)
            return Result<string>.Failure(Error.InvalidName(fullPath, "Path exceeds system limits"));

        // Validate each component of the path
        try
        {
            var fileName = Path.GetFileName(fullPath);
            if (!string.IsNullOrEmpty(fileName))
            {
                var fileNameValidation = Validate(fileName);
                if (fileNameValidation.IsFailure)
                    return fileNameValidation;
            }

            return Result<string>.Success(fullPath);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(Error.InvalidName(fullPath, $"Path validation failed: {ex.Message}"));
        }
    }

    private static bool IsReservedName(string nameWithoutExtension)
    {
        return !string.IsNullOrEmpty(nameWithoutExtension) &&
               ReservedNames.Contains(nameWithoutExtension, StringComparer.OrdinalIgnoreCase);
    }
}