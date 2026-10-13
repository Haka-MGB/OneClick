using System.IO;

namespace OneClick.Domain.Common;

/// <summary>
/// Represents a typed error that can occur during operations.
/// Used instead of exceptions for expected error conditions.
/// </summary>
public sealed class Error
{
    /// <summary>
    /// Initializes a new error.
    /// </summary>
    /// <param name="type">The category of error</param>
    /// <param name="message">Human-readable error message</param>
    /// <param name="details">Additional technical details</param>
    public Error(ErrorType type, string message, string? details = null)
    {
        Type = type;
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Details = details;
    }

    /// <summary>
    /// The category of error.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    /// Human-readable error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Additional technical details about the error.
    /// </summary>
    public string? Details { get; }

    /// <summary>
    /// Creates a protected path error.
    /// </summary>
    public static Error ProtectedPath(string path) =>
        new(ErrorType.ProtectedPath, $"Cannot modify protected location: {path}");

    /// <summary>
    /// Creates a file locked error.
    /// </summary>
    public static Error Locked(string path) =>
        new(ErrorType.Locked, $"File is locked or in use: {Path.GetFileName(path)}", path);

    /// <summary>
    /// Creates a permission denied error.
    /// </summary>
    public static Error PermissionDenied(string path) =>
        new(ErrorType.PermissionDenied, $"Access denied to: {Path.GetFileName(path)}", path);

    /// <summary>
    /// Creates a file not found error.
    /// </summary>
    public static Error NotFound(string path) =>
        new(ErrorType.NotFound, $"File not found: {Path.GetFileName(path)}", path);

    /// <summary>
    /// Creates an invalid name error.
    /// </summary>
    public static Error InvalidName(string name, string reason) =>
        new(ErrorType.InvalidName, $"Invalid file name '{name}': {reason}");

    /// <summary>
    /// Creates a name conflict error.
    /// </summary>
    public static Error Conflict(string path) =>
        new(ErrorType.Conflict, $"Destination already exists: {Path.GetFileName(path)}", path);

    /// <summary>
    /// Creates a cancelled operation error.
    /// </summary>
    public static Error Cancelled() =>
        new(ErrorType.Cancelled, "Operation was cancelled");

    /// <summary>
    /// Creates a general error.
    /// </summary>
    public static Error General(string message, string? details = null) =>
        new(ErrorType.Unexpected, message, details);

    /// <summary>
    /// Creates an unexpected error from an exception.
    /// </summary>
    public static Error FromException(Exception exception) =>
        new(ErrorType.Unexpected, exception.Message, exception.ToString());

    public override string ToString() => Details != null ? $"{Message} ({Details})" : Message;
}

/// <summary>
/// Categories of errors that can occur during file operations.
/// </summary>
public enum ErrorType
{
    /// <summary>
    /// Attempt to modify a protected system location.
    /// </summary>
    ProtectedPath,

    /// <summary>
    /// File is locked by another process.
    /// </summary>
    Locked,

    /// <summary>
    /// Insufficient permissions to access the file.
    /// </summary>
    PermissionDenied,

    /// <summary>
    /// File or directory does not exist.
    /// </summary>
    NotFound,

    /// <summary>
    /// Invalid file or directory name.
    /// </summary>
    InvalidName,

    /// <summary>
    /// Destination already exists and would be overwritten.
    /// </summary>
    Conflict,

    /// <summary>
    /// Operation was cancelled by user or system.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Unexpected error or system failure.
    /// </summary>
    Unexpected
}