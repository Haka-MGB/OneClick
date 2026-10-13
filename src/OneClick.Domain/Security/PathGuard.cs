using System.IO;

namespace OneClick.Domain.Security;

/// <summary>
/// Validates whether paths are safe to access based on security rules.
/// Implementation of FR-CORE-06: Protected locations shall never be modified.
/// </summary>
public sealed class PathGuard
{
    private readonly HashSet<string> _protectedPaths;

    /// <summary>
    /// Initializes a new path guard with default protected locations.
    /// </summary>
    public PathGuard()
    {
        _protectedPaths = GetDefaultProtectedPaths();
    }

    /// <summary>
    /// Initializes a path guard with custom protected paths.
    /// </summary>
    /// <param name="protectedPaths">Set of protected path roots</param>
    public PathGuard(IEnumerable<string> protectedPaths)
    {
        _protectedPaths = protectedPaths?.Select(NormalizePath).Where(p => !string.IsNullOrEmpty(p)).ToHashSet(StringComparer.OrdinalIgnoreCase) 
            ?? throw new ArgumentNullException(nameof(protectedPaths));
    }

    /// <summary>
    /// Checks if a path operation is allowed.
    /// </summary>
    /// <param name="path">The path to validate</param>
    /// <param name="intent">What the caller intends to do with the path</param>
    /// <returns>Result indicating whether the operation is allowed</returns>
    public GuardResult Check(string path, GuardIntent intent)
    {
        if (string.IsNullOrWhiteSpace(path))
            return GuardResult.Deny("Path cannot be empty");

        try
        {
            var rawPatternResult = CheckPathPatterns(path);
            if (!rawPatternResult.Allowed)
                return rawPatternResult;

            // Resolve to canonical path to handle relative paths, symlinks, etc.
            var canonicalPath = NormalizePath(path);
            if (string.IsNullOrEmpty(canonicalPath))
                return GuardResult.Deny("Invalid path format");

            // Check for protected locations
            var protectedViolation = CheckProtectedPaths(canonicalPath, intent);
            if (!protectedViolation.Allowed)
                return protectedViolation;

            // Check for dangerous path patterns
            var pathPatternResult = CheckPathPatterns(canonicalPath);
            if (!pathPatternResult.Allowed)
                return pathPatternResult;

            // Check file attributes if the file exists
            if (File.Exists(canonicalPath))
            {
                var attributeResult = CheckFileAttributes(canonicalPath, intent);
                if (!attributeResult.Allowed)
                    return attributeResult;
            }

            return GuardResult.Allow();
        }
        catch (Exception ex)
        {
            return GuardResult.Deny($"Path validation failed: {ex.Message}");
        }
    }

    private GuardResult CheckProtectedPaths(string canonicalPath, GuardIntent intent)
    {
        // For write operations, be more restrictive
        if (intent == GuardIntent.WriteTarget || intent == GuardIntent.ReadForChange)
        {
            // Don't allow any operations in protected directories
            foreach (var protectedPath in _protectedPaths)
            {
                if (IsUnderPath(canonicalPath, protectedPath))
                {
                    return GuardResult.Deny($"Cannot modify files in protected location: {protectedPath}");
                }
            }

            // Don't allow operations on drive roots
            if (IsDriveRoot(canonicalPath))
            {
                return GuardResult.Deny("Cannot modify drive root");
            }

            // Don't allow operations on user profile root (but allow subdirectories)
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(userProfile) && 
                string.Equals(canonicalPath, NormalizePath(userProfile), StringComparison.OrdinalIgnoreCase))
            {
                return GuardResult.Deny("Cannot modify user profile root directory");
            }
        }

        return GuardResult.Allow();
    }

    private static GuardResult CheckPathPatterns(string canonicalPath)
    {
        // Check for path traversal attempts
        if (canonicalPath.Contains("..") || canonicalPath.Contains("./") || canonicalPath.Contains(".\\"))
        {
            return GuardResult.Deny("Path contains traversal patterns");
        }

        // Check for reserved device names
        var fileName = Path.GetFileName(canonicalPath);
        if (IsReservedDeviceName(fileName))
        {
            return GuardResult.Deny($"'{fileName}' is a reserved device name");
        }

        return GuardResult.Allow();
    }

    private static GuardResult CheckFileAttributes(string path, GuardIntent intent)
    {
        try
        {
            var attributes = File.GetAttributes(path);

            // Never follow reparse points (junctions, symlinks)
            if ((attributes & FileAttributes.ReparsePoint) != 0)
            {
                return GuardResult.Deny("Cannot follow symbolic links or junctions");
            }

            // Be cautious with system files for write operations
            if (intent != GuardIntent.ScanOnly && (attributes & FileAttributes.System) != 0)
            {
                return GuardResult.Deny("Cannot modify system files");
            }

            return GuardResult.Allow();
        }
        catch
        {
            // If we can't read attributes, deny for safety
            return GuardResult.Deny("Cannot read file attributes");
        }
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static bool IsUnderPath(string childPath, string parentPath)
    {
        if (string.IsNullOrEmpty(childPath) || string.IsNullOrEmpty(parentPath))
            return false;

        // Exact match
        if (string.Equals(childPath, parentPath, StringComparison.OrdinalIgnoreCase))
            return true;

        // Child path check - ensure we match on directory boundaries
        return childPath.StartsWith(parentPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsDriveRoot(string path)
    {
        try
        {
            var root = Path.GetPathRoot(path);
            return !string.IsNullOrEmpty(root) && 
                   string.Equals(path, root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsReservedDeviceName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;

        // Remove extension for the check
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        
        var reservedNames = new[]
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        return reservedNames.Contains(nameWithoutExtension, StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> GetDefaultProtectedPaths()
    {
        var paths = new List<string>();

        // System directories
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.System);
        if (!string.IsNullOrEmpty(systemRoot))
        {
            paths.Add(Path.GetDirectoryName(systemRoot) ?? systemRoot); // Usually C:\Windows
        }

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            paths.Add(programFiles);
        }

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86))
        {
            paths.Add(programFilesX86);
        }

        // Convert to normalized paths
        return paths.Select(NormalizePath)
            .Where(p => !string.IsNullOrEmpty(p))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
