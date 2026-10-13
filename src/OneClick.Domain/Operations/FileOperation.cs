using System.IO;

namespace OneClick.Domain.Operations;

/// <summary>
/// Represents a single planned file operation.
/// Immutable value object that describes what will happen to one file.
/// </summary>
/// <param name="Type">The type of operation to perform</param>
/// <param name="Source">The current path of the file</param>
/// <param name="Destination">The target path after the operation</param>
/// <param name="Size">The file size in bytes</param>
/// <param name="ModifiedUtc">When the file was last modified</param>
public sealed record FileOperation(
    OperationType Type,
    string Source,
    string Destination,
    long Size,
    DateTime ModifiedUtc)
{
    /// <summary>
    /// Creates a Move operation.
    /// </summary>
    public static FileOperation Move(string source, string destination, long size, DateTime modifiedUtc) =>
        new(OperationType.Move, source, destination, size, modifiedUtc);

    /// <summary>
    /// Creates a Rename operation.
    /// </summary>
    public static FileOperation Rename(string source, string destination, long size, DateTime modifiedUtc) =>
        new(OperationType.Rename, source, destination, size, modifiedUtc);

    /// <summary>
    /// Creates a Hold operation (move to holding area).
    /// </summary>
    public static FileOperation Hold(string source, string destination, long size, DateTime modifiedUtc) =>
        new(OperationType.Hold, source, destination, size, modifiedUtc);

    /// <summary>
    /// Gets the file name from the source path.
    /// </summary>
    public string SourceFileName => Path.GetFileName(Source);

    /// <summary>
    /// Gets the file name from the destination path.
    /// </summary>
    public string DestinationFileName => Path.GetFileName(Destination);

    /// <summary>
    /// Determines if this is a cross-drive operation.
    /// </summary>
    public bool IsCrossDrive
    {
        get
        {
            try
            {
                var sourceDrive = Path.GetPathRoot(Path.GetFullPath(Source));
                var destDrive = Path.GetPathRoot(Path.GetFullPath(Destination));
                return !string.Equals(sourceDrive, destDrive, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                // If we can't determine drives, assume cross-drive for safety
                return true;
            }
        }
    }
}