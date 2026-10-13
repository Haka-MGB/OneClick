namespace OneClick.Domain.Operations;

/// <summary>
/// Defines the types of file operations that can be performed.
/// </summary>
public enum OperationType
{
    /// <summary>
    /// Move a file from one location to another (same or different drive).
    /// </summary>
    Move,

    /// <summary>
    /// Rename a file in place (change name but keep same directory).
    /// </summary>
    Rename,

    /// <summary>
    /// Move a file to the holding area for safe deletion.
    /// </summary>
    Hold
}