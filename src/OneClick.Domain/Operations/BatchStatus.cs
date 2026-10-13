namespace OneClick.Domain.Operations;

/// <summary>
/// Represents the current status of an operation batch.
/// </summary>
public enum BatchStatus
{
    /// <summary>
    /// Batch has been planned but not yet executed.
    /// </summary>
    Planned,

    /// <summary>
    /// Batch is currently being executed.
    /// </summary>
    Running,

    /// <summary>
    /// Batch completed successfully - all operations executed.
    /// </summary>
    Complete,

    /// <summary>
    /// Batch failed - some or all operations could not be completed.
    /// </summary>
    Failed,

    /// <summary>
    /// Batch was cancelled during execution.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Batch has been undone - all operations reversed.
    /// </summary>
    Undone,

    /// <summary>
    /// Batch was partially undone - some operations could not be reversed.
    /// </summary>
    PartiallyUndone
}