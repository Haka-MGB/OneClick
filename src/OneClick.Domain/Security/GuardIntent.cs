namespace OneClick.Domain.Security;

/// <summary>
/// Describes the intended use of a path for security validation.
/// </summary>
public enum GuardIntent
{
    /// <summary>
    /// Reading a path to determine what files to modify.
    /// </summary>
    ReadForChange,

    /// <summary>
    /// Writing to a path as the target of an operation.
    /// </summary>
    WriteTarget,

    /// <summary>
    /// Scanning a path for analysis only (no modifications).
    /// </summary>
    ScanOnly
}