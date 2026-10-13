using System.IO;
using OneClick.Domain.Common;

namespace OneClick.Domain.FileSystem;

/// <summary>
/// Resolves file name conflicts by generating unique names.
/// Implements FR-CORE-07: Never overwrite files, use numeric suffixes.
/// </summary>
public sealed class ConflictResolver
{
    private readonly Func<string, bool> _fileExists;
    private readonly HashSet<string> _plannedNames;

    /// <summary>
    /// Initializes a new conflict resolver.
    /// </summary>
    /// <param name="fileExists">Function to check if a file exists</param>
    public ConflictResolver(Func<string, bool> fileExists)
    {
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _plannedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets a unique file name that doesn't conflict with existing files or planned operations.
    /// </summary>
    /// <param name="desiredPath">The desired file path</param>
    /// <returns>A unique path that doesn't conflict</returns>
    public Result<string> GetUniquePath(string desiredPath)
    {
        if (string.IsNullOrWhiteSpace(desiredPath))
            return Result<string>.Failure(Error.InvalidName(desiredPath ?? "", "Path cannot be empty"));

        try
        {
            var directory = Path.GetDirectoryName(desiredPath) ?? "";
            var fileName = Path.GetFileNameWithoutExtension(desiredPath);
            var extension = Path.GetExtension(desiredPath);

            // Validate the original name
            var nameValidation = NameSanitizer.Validate(Path.GetFileName(desiredPath));
            if (nameValidation.IsFailure)
                return Result<string>.Failure(nameValidation.Error);

            var candidatePath = desiredPath;

            // If the path doesn't conflict, use it as-is
            if (!HasConflict(candidatePath))
            {
                RegisterPlannedName(candidatePath);
                return Result<string>.Success(candidatePath);
            }

            // Generate unique name with numeric suffix
            for (int suffix = 1; suffix <= 9999; suffix++)
            {
                // Remove any existing suffix before adding new one
                var baseName = RemoveExistingSuffix(fileName);
                var uniqueName = $"{baseName} ({suffix}){extension}";
                candidatePath = Path.Combine(directory, uniqueName);

                // Validate the generated name
                var uniqueNameValidation = NameSanitizer.Validate(uniqueName);
                if (uniqueNameValidation.IsFailure)
                    continue; // Try next suffix

                if (!HasConflict(candidatePath))
                {
                    RegisterPlannedName(candidatePath);
                    return Result<string>.Success(candidatePath);
                }
            }

            return Result<string>.Failure(Error.Conflict($"Unable to generate unique name for {desiredPath}"));
        }
        catch (Exception ex)
        {
            return Result<string>.Failure(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Registers a name as being planned for use (to avoid conflicts within the same batch).
    /// </summary>
    /// <param name="path">The path being planned</param>
    public void RegisterPlannedName(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            _plannedNames.Add(path);
        }
    }

    /// <summary>
    /// Handles case-only renames which require special treatment on Windows.
    /// </summary>
    /// <param name="sourcePath">Current file path</param>
    /// <param name="desiredPath">Desired new path</param>
    /// <returns>Rename strategy: direct rename, two-phase rename, or error</returns>
    public Result<RenameStrategy> GetRenameStrategy(string sourcePath, string desiredPath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(desiredPath))
            return Result<RenameStrategy>.Failure("Source and destination paths cannot be empty");

        try
        {
            var sourceDir = Path.GetDirectoryName(sourcePath);
            var destDir = Path.GetDirectoryName(desiredPath);

            // Different directories = move operation
            if (!string.Equals(sourceDir, destDir, StringComparison.OrdinalIgnoreCase))
            {
                return Result<RenameStrategy>.Success(RenameStrategy.DirectRename(desiredPath));
            }

            var sourceName = Path.GetFileName(sourcePath);
            var destName = Path.GetFileName(desiredPath);

            // Same name = no operation needed
            if (string.Equals(sourceName, destName, StringComparison.Ordinal))
            {
                return Result<RenameStrategy>.Success(RenameStrategy.NoOperation());
            }

            // Case-only change = needs two-phase rename on case-insensitive file systems
            if (string.Equals(sourceName, destName, StringComparison.OrdinalIgnoreCase))
            {
                var tempPath = GenerateTemporaryPath(sourcePath);
                return Result<RenameStrategy>.Success(RenameStrategy.TwoPhaseRename(tempPath, desiredPath));
            }

            // Different name = direct rename
            return Result<RenameStrategy>.Success(RenameStrategy.DirectRename(desiredPath));
        }
        catch (Exception ex)
        {
            return Result<RenameStrategy>.Failure(Error.FromException(ex));
        }
    }

    /// <summary>
    /// Clears all planned names (typically called after a batch is executed or cancelled).
    /// </summary>
    public void ClearPlannedNames()
    {
        _plannedNames.Clear();
    }

    private bool HasConflict(string path)
    {
        return _fileExists(path) || _plannedNames.Contains(path);
    }

    private static string RemoveExistingSuffix(string fileName)
    {
        // Remove patterns like " (1)", " (2)", etc.
        var pattern = @" \(\d+\)$";
        return System.Text.RegularExpressions.Regex.Replace(fileName, pattern, "");
    }

    private static string GenerateTemporaryPath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? "";
        var extension = Path.GetExtension(originalPath);
        var tempName = $".oneclick-tmp-{Guid.NewGuid():N}{extension}";
        return Path.Combine(directory, tempName);
    }
}

/// <summary>
/// Describes the strategy needed for a rename operation.
/// </summary>
public sealed class RenameStrategy
{
    private RenameStrategy(RenameType type, string? temporaryPath, string? finalPath)
    {
        Type = type;
        TemporaryPath = temporaryPath;
        FinalPath = finalPath;
    }

    /// <summary>
    /// The type of rename strategy.
    /// </summary>
    public RenameType Type { get; }

    /// <summary>
    /// Temporary path for two-phase renames (if applicable).
    /// </summary>
    public string? TemporaryPath { get; }

    /// <summary>
    /// Final destination path.
    /// </summary>
    public string? FinalPath { get; }

    /// <summary>
    /// Creates a direct rename strategy.
    /// </summary>
    public static RenameStrategy DirectRename(string finalPath) =>
        new(RenameType.Direct, null, finalPath);

    /// <summary>
    /// Creates a two-phase rename strategy for case changes.
    /// </summary>
    public static RenameStrategy TwoPhaseRename(string temporaryPath, string finalPath) =>
        new(RenameType.TwoPhase, temporaryPath, finalPath);

    /// <summary>
    /// Creates a no-operation strategy when no rename is needed.
    /// </summary>
    public static RenameStrategy NoOperation() =>
        new(RenameType.None, null, null);
}

/// <summary>
/// Types of rename strategies.
/// </summary>
public enum RenameType
{
    /// <summary>
    /// No rename needed.
    /// </summary>
    None,

    /// <summary>
    /// Direct rename from source to destination.
    /// </summary>
    Direct,

    /// <summary>
    /// Two-phase rename: source -> temp -> destination (for case changes).
    /// </summary>
    TwoPhase
}
