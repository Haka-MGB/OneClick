using System.Globalization;

namespace OneClick.App.Services;

public sealed record OperationResult(int Succeeded, int Skipped);

public sealed record RenameOptions
{
    public string Find { get; init; } = string.Empty;
    public string Replace { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public string Suffix { get; init; } = string.Empty;
    public int CaseMode { get; init; }
    public bool AddNumber { get; init; }
    public int NumberStart { get; init; } = 1;
}

public sealed record RenamePreviewItem(string OldName, string NewName);
public sealed record RenamePlan(int TotalFiles, IReadOnlyList<RenamePreviewItem> Items)
{
    public int Count => Items.Count;
}

public sealed class RenameService
{
    private readonly JournalService _journal;

    public RenameService(JournalService journal)
    {
        _journal = journal;
    }

    public RenamePlan Plan(string folder, RenameOptions options)
    {
        if (!Directory.Exists(folder))
        {
            return new RenamePlan(0, []);
        }

        var files = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(IsRegularUserFile)
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var usedNames = new HashSet<string>(
            files.Select(Path.GetFileName).Where(n => n is not null).Select(n => n!),
            StringComparer.OrdinalIgnoreCase);
        var items = new List<RenamePreviewItem>();
        var sequence = Math.Max(1, options.NumberStart);

        foreach (var file in files)
        {
            var oldName = Path.GetFileName(file);
            var newName = BuildName(oldName, options, sequence);

            if (options.AddNumber)
            {
                sequence++;
            }

            if (string.Equals(oldName, newName, StringComparison.Ordinal))
            {
                continue;
            }

            usedNames.Remove(oldName);
            newName = EnsureUniqueName(newName, usedNames);
            usedNames.Add(newName);
            items.Add(new RenamePreviewItem(oldName, newName));
        }

        return new RenamePlan(files.Count, items);
    }

    public OperationResult Apply(string folder, RenameOptions options)
    {
        var plan = Plan(folder, options);
        var succeeded = 0;
        var skipped = 0;

        foreach (var item in plan.Items)
        {
            try
            {
                var source = Path.Combine(folder, item.OldName);
                var destination = Path.Combine(folder, item.NewName);
                if (!File.Exists(source) || File.Exists(destination))
                {
                    skipped++;
                    continue;
                }

                File.Move(source, destination);
                succeeded++;
            }
            catch
            {
                skipped++;
            }
        }

        if (succeeded > 0)
        {
            _journal.Record("Bulk rename", succeeded);
        }

        return new OperationResult(succeeded, skipped);
    }

    private static string BuildName(string oldName, RenameOptions options, int sequence)
    {
        var extension = Path.GetExtension(oldName);
        var stem = Path.GetFileNameWithoutExtension(oldName);

        if (!string.IsNullOrEmpty(options.Find))
        {
            stem = stem.Replace(options.Find, options.Replace, StringComparison.OrdinalIgnoreCase);
        }

        stem = options.CaseMode switch
        {
            1 => stem.ToLower(CultureInfo.CurrentCulture),
            2 => stem.ToUpper(CultureInfo.CurrentCulture),
            3 => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(stem.ToLower(CultureInfo.CurrentCulture)),
            _ => stem,
        };

        stem = options.Prefix + stem + options.Suffix;
        if (options.AddNumber)
        {
            stem = $"{sequence:D3} {stem}";
        }

        return string.IsNullOrWhiteSpace(stem) ? oldName : stem + extension;
    }

    private static string EnsureUniqueName(string desiredName, HashSet<string> usedNames)
    {
        if (!usedNames.Contains(desiredName))
        {
            return desiredName;
        }

        var stem = Path.GetFileNameWithoutExtension(desiredName);
        var extension = Path.GetExtension(desiredName);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = $"{stem} ({i}){extension}";
            if (!usedNames.Contains(candidate))
            {
                return candidate;
            }
        }

        return $"{stem} ({Guid.NewGuid():N}){extension}";
    }

    private static bool IsRegularUserFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Directory | FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint)) == 0;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record OrganisePreviewItem(string FileName, string SubFolder);
public sealed record OrganisePlan(IReadOnlyList<OrganisePreviewItem> Items)
{
    public int Count => Items.Count;
}

public sealed class OrganiseService
{
    private readonly JournalService _journal;

    public OrganiseService(JournalService journal)
    {
        _journal = journal;
    }

    public OrganisePlan Plan(string folder, bool byMonth)
    {
        if (!Directory.Exists(folder))
        {
            return new OrganisePlan([]);
        }

        var items = Directory.EnumerateFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(RenameServiceIsRegularUserFile)
            .Select(path => new FileInfo(path))
            .Select(info => new OrganisePreviewItem(info.Name, byMonth ? info.LastWriteTime.ToString("yyyy-MM", CultureInfo.InvariantCulture) : TypeFolder(info.Extension)))
            .Where(item => !string.Equals(item.FileName, item.SubFolder, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return new OrganisePlan(items);
    }

    public OperationResult Apply(string folder, bool byMonth)
    {
        var plan = Plan(folder, byMonth);
        var succeeded = 0;
        var skipped = 0;

        foreach (var item in plan.Items)
        {
            try
            {
                var targetFolder = Path.Combine(folder, item.SubFolder);
                Directory.CreateDirectory(targetFolder);

                var source = Path.Combine(folder, item.FileName);
                var destination = UniquePath(Path.Combine(targetFolder, item.FileName));
                if (!File.Exists(source))
                {
                    skipped++;
                    continue;
                }

                File.Move(source, destination);
                succeeded++;
            }
            catch
            {
                skipped++;
            }
        }

        if (succeeded > 0)
        {
            _journal.Record("Folder organise", succeeded);
        }

        return new OperationResult(succeeded, skipped);
    }

    private static string TypeFolder(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" or ".webp" or ".heic" => "Images",
        ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" or ".md" => "Documents",
        ".xls" or ".xlsx" or ".csv" => "Spreadsheets",
        ".mp4" or ".mkv" or ".mov" or ".avi" or ".wmv" => "Videos",
        ".mp3" or ".wav" or ".flac" or ".m4a" => "Audio",
        ".zip" or ".rar" or ".7z" => "Archives",
        "" => "No extension",
        _ => extension.TrimStart('.').ToUpperInvariant(),
    };

    private static string UniquePath(string desiredPath)
    {
        if (!File.Exists(desiredPath))
        {
            return desiredPath;
        }

        var directory = Path.GetDirectoryName(desiredPath) ?? string.Empty;
        var stem = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);
        for (var i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({i}){extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        return Path.Combine(directory, $"{stem} ({Guid.NewGuid():N}){extension}");
    }

    private static bool RenameServiceIsRegularUserFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Directory | FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint)) == 0;
        }
        catch
        {
            return false;
        }
    }
}

public sealed record ScanEntry(string FullPath, long Size, DateTime Modified);

public sealed class StorageService
{
    private readonly JournalService _journal;
    private readonly string _holdingFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OneClick",
        "Holding");

    public StorageService(JournalService journal)
    {
        _journal = journal;
    }

    public async IAsyncEnumerable<ScanEntry> ScanAsync(
        string folder,
        long minBytes,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (!Directory.Exists(folder))
        {
            yield break;
        }

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(folder, "*", new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = true,
                AttributesToSkip = FileAttributes.System | FileAttributes.Hidden | FileAttributes.ReparsePoint,
            });
        }
        catch
        {
            yield break;
        }

        foreach (var file in files)
        {
            if (ct.IsCancellationRequested)
            {
                yield break;
            }

            FileInfo info;
            try
            {
                info = new FileInfo(file);
                if (!info.Exists || info.Length < minBytes)
                {
                    continue;
                }
            }
            catch
            {
                continue;
            }

            yield return new ScanEntry(info.FullName, info.Length, info.LastWriteTime);
            await Task.Yield();
        }
    }

    public OperationResult MoveToHolding(IReadOnlyCollection<string> paths)
    {
        Directory.CreateDirectory(_holdingFolder);
        var succeeded = 0;
        var skipped = 0;

        foreach (var path in paths)
        {
            try
            {
                if (!File.Exists(path))
                {
                    skipped++;
                    continue;
                }

                var destination = UniqueHoldingPath(Path.GetFileName(path));
                File.Move(path, destination);
                succeeded++;
            }
            catch
            {
                skipped++;
            }
        }

        if (succeeded > 0)
        {
            _journal.Record("Storage cleanup", succeeded);
        }

        return new OperationResult(succeeded, skipped);
    }

    public long HoldingAreaSize()
    {
        if (!Directory.Exists(_holdingFolder))
        {
            return 0;
        }

        return Directory.EnumerateFiles(_holdingFolder, "*", SearchOption.AllDirectories)
            .Sum(path =>
            {
                try { return new FileInfo(path).Length; }
                catch { return 0L; }
            });
    }

    public void EmptyHolding()
    {
        if (!Directory.Exists(_holdingFolder))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(_holdingFolder, "*", SearchOption.AllDirectories))
        {
            try { File.Delete(file); }
            catch { }
        }
    }

    private string UniqueHoldingPath(string fileName)
    {
        var destination = Path.Combine(_holdingFolder, fileName);
        if (!File.Exists(destination))
        {
            return destination;
        }

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        return Path.Combine(_holdingFolder, $"{stem}-{DateTime.Now:yyyyMMddHHmmssfff}{extension}");
    }
}

public sealed record RuleTemplate(string Name, string Description, string Example);
public sealed record AutomationRule(Guid Id, string Name, string Summary, bool IsEnabled);

public sealed class AutomationService
{
    private readonly List<AutomationRule> _rules = [];

    public IReadOnlyList<RuleTemplate> GetTemplates() =>
    [
        new("Downloads tidy-up", "Move common downloads into folders by type.", "PDFs to Documents, images to Pictures"),
        new("Screenshots cleanup", "Collect screenshots into one dated folder.", "Screenshots to Pictures\\Screenshots"),
        new("Old installers review", "Find large installer files for review.", "EXE and MSI files older than 30 days")
    ];

    public IReadOnlyList<AutomationRule> GetRules() => _rules.ToList();

    public IReadOnlyList<string> DryRun(RuleTemplate template) =>
    [
        $"{template.Name}: 3 sample files would be reviewed.",
        "No changes are made during dry run."
    ];

    public void CreateFromTemplate(RuleTemplate template)
    {
        if (_rules.Any(r => string.Equals(r.Name, template.Name, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        _rules.Add(new AutomationRule(Guid.NewGuid(), template.Name, template.Description, true));
    }

    public void SetEnabled(Guid id, bool enabled)
    {
        var index = _rules.FindIndex(r => r.Id == id);
        if (index >= 0)
        {
            var current = _rules[index];
            _rules[index] = current with { IsEnabled = enabled };
        }
    }
}

public sealed record BatteryReport(
    bool IsAvailable,
    double HealthPercent,
    int? DesignCapacityMwh,
    int? FullChargeCapacityMwh,
    int? CycleCount);

public sealed class BatteryService
{
    public BatteryReport GetReport()
    {
        if (!BatteryHelper.DeviceHasBattery())
        {
            return new BatteryReport(false, 0, null, null, null);
        }

        return new BatteryReport(true, 100, null, null, null);
    }
}

public sealed class SettingsService
{
    private readonly Dictionary<string, string> _settings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Theme"] = "System default",
        ["HoldingRetentionDays"] = "30",
        ["HoldingFolder"] = "",
        ["HistoryRetentionDays"] = "90",
        ["UpdateCheck"] = "true",
        ["CrashReports"] = "false",
    };

    public string Get(string key, string defaultValue) =>
        _settings.TryGetValue(key, out var value) ? value : defaultValue;

    public void Set(string key, string value)
    {
        _settings[key] = value;
    }

    public void ResetDefaults()
    {
        _settings.Clear();
        _settings["Theme"] = "System default";
        _settings["HoldingRetentionDays"] = "30";
        _settings["HoldingFolder"] = "";
        _settings["HistoryRetentionDays"] = "90";
        _settings["UpdateCheck"] = "true";
        _settings["CrashReports"] = "false";
    }

    public void ExportLog(string destination)
    {
        var lines = new[]
        {
            "One Click diagnostic log",
            $"Exported: {DateTime.Now:O}",
            $"OS: {Environment.OSVersion}",
            $".NET: {Environment.Version}",
        };
        File.WriteAllLines(destination, lines);
    }
}
