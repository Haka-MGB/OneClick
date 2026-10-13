using System.Collections.ObjectModel;

namespace OneClick.Domain.Operations;

/// <summary>
/// Represents a labeled set of file operations from one user action or rule run.
/// This is the unit of work for the operation engine and undo system.
/// </summary>
public sealed class OperationBatch
{
    private readonly List<FileOperation> _operations;

    /// <summary>
    /// Initializes a new operation batch.
    /// </summary>
    /// <param name="id">Unique identifier for this batch</param>
    /// <param name="tool">The tool or feature that created this batch</param>
    /// <param name="label">Human-readable description of what this batch does</param>
    /// <param name="createdUtc">When this batch was created</param>
    /// <param name="operations">The operations to perform</param>
    public OperationBatch(
        Guid id,
        string tool,
        string label,
        DateTime createdUtc,
        IEnumerable<FileOperation> operations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);

        Id = id;
        Tool = tool;
        Label = label;
        CreatedUtc = createdUtc;
        _operations = operations?.ToList() ?? throw new ArgumentNullException(nameof(operations));
        Status = BatchStatus.Planned;
    }

    /// <summary>
    /// Unique identifier for this batch.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// The tool or feature that created this batch (e.g., "Bulk Rename", "Folder Organiser").
    /// </summary>
    public string Tool { get; }

    /// <summary>
    /// Human-readable description of what this batch does (e.g., "Rename 48 files").
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// When this batch was created.
    /// </summary>
    public DateTime CreatedUtc { get; }

    /// <summary>
    /// Current status of the batch.
    /// </summary>
    public BatchStatus Status { get; private set; }

    /// <summary>
    /// When the batch status was last updated.
    /// </summary>
    public DateTime? UpdatedUtc { get; private set; }

    /// <summary>
    /// Additional notes about the batch (e.g., error messages, cancellation reason).
    /// </summary>
    public string? Notes { get; private set; }

    /// <summary>
    /// Read-only collection of operations in this batch.
    /// </summary>
    public ReadOnlyCollection<FileOperation> Operations => _operations.AsReadOnly();

    /// <summary>
    /// Number of operations in this batch.
    /// </summary>
    public int OperationCount => _operations.Count;

    /// <summary>
    /// Total size of all files in this batch.
    /// </summary>
    public long TotalSize => _operations.Sum(op => op.Size);

    /// <summary>
    /// Updates the batch status.
    /// </summary>
    public void UpdateStatus(BatchStatus status, string? notes = null)
    {
        Status = status;
        UpdatedUtc = DateTime.UtcNow;
        Notes = notes;
    }

    /// <summary>
    /// Creates a new batch with the specified operations.
    /// </summary>
    public static OperationBatch Create(
        string tool,
        string label,
        IEnumerable<FileOperation> operations)
    {
        return new OperationBatch(
            Guid.NewGuid(),
            tool,
            label,
            DateTime.UtcNow,
            operations);
    }

    /// <summary>
    /// Determines if this batch can be undone based on its current status.
    /// </summary>
    public bool CanUndo => Status is BatchStatus.Complete or BatchStatus.Failed;

    /// <summary>
    /// Determines if this batch conflicts with another batch for undo purposes.
    /// Two batches conflict if one moves a file that the other also operates on.
    /// </summary>
    public bool ConflictsWith(OperationBatch other)
    {
        if (other == null) return false;

        var myPaths = _operations.SelectMany(op => new[] { op.Source, op.Destination }).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var otherPaths = other._operations.SelectMany(op => new[] { op.Source, op.Destination }).ToHashSet(StringComparer.OrdinalIgnoreCase);

        return myPaths.Overlaps(otherPaths);
    }
}