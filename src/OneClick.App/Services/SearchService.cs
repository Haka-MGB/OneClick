namespace OneClick.App.Services;

public record SearchOptions
{
    public string Query      { get; init; } = "";
    public int    DateFilter { get; init; } // 0=Any, 1=Today, 2=Week, 3=Month, 4=30d, 5=90d
    public string TypeFilter { get; init; } = "All types";
    public int    SizeFilter { get; init; } // 0=Any, 1=<1MB, 2=1-10MB, 3=10-100MB, 4=>100MB
}

public record SearchResult(string FileName, string Directory, string FullPath, long Size, DateTime Modified);

public class SearchService
{
    private static readonly Dictionary<string, string[]> TypeExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Images"]       = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg", ".heic", ".tiff"],
        ["Documents"]    = [".pdf", ".doc", ".docx", ".txt", ".rtf", ".odt", ".ppt", ".pptx", ".md"],
        ["Spreadsheets"] = [".xls", ".xlsx", ".csv", ".ods"],
        ["Video"]        = [".mp4", ".mkv", ".mov", ".avi", ".wmv", ".webm"],
        ["Audio"]        = [".mp3", ".wav", ".flac", ".m4a", ".aac", ".ogg"],
        ["Archives"]     = [".zip", ".rar", ".7z", ".tar", ".gz"],
        ["Installers"]   = [".exe", ".msi", ".msix"],
        ["Code"]         = [".py", ".js", ".ts", ".html", ".css", ".java", ".cs", ".cpp", ".c", ".json", ".sql"],
    };

    public async IAsyncEnumerable<SearchResult> SearchAsync(
        SearchOptions opts,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var roots = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),
        };

        var cutoff = opts.DateFilter switch
        {
            1 => DateTime.Today,
            2 => DateTime.Today.AddDays(-7),
            3 => DateTime.Today.AddDays(-30),
            4 => DateTime.Today.AddDays(-30),
            5 => DateTime.Today.AddDays(-90),
            _ => DateTime.MinValue,
        };

        var extensions = opts.TypeFilter != "All types" && TypeExtensions.TryGetValue(opts.TypeFilter, out var exts)
            ? exts
            : null;

        foreach (var root in roots.Where(Directory.Exists))
        {
            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(root, "*", new EnumerationOptions
                {
                    IgnoreInaccessible  = true,
                    RecurseSubdirectories = true,
                    AttributesToSkip    = FileAttributes.System | FileAttributes.ReparsePoint,
                });
            }
            catch { continue; }

            foreach (var file in files)
            {
                if (ct.IsCancellationRequested) yield break;

                SearchResult? result = null;
                try
                {
                    var name = Path.GetFileName(file);

                    // Name filter
                    if (!string.IsNullOrEmpty(opts.Query) &&
                        !name.Contains(opts.Query, StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Extension filter
                    var ext = Path.GetExtension(file);
                    if (extensions is not null && !extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                        continue;

                    var info = new FileInfo(file);

                    // Date filter
                    if (info.LastWriteTime < cutoff) continue;

                    // Size filter
                    if (!PassesSizeFilter(info.Length, opts.SizeFilter)) continue;

                    result = new SearchResult(name, Path.GetDirectoryName(file) ?? "", file, info.Length, info.LastWriteTime);
                }
                catch { /* skip inaccessible files */ }

                if (result is not null)
                {
                    yield return result;
                    await Task.Yield();
                }
            }
        }
    }

    private static bool PassesSizeFilter(long bytes, int filter) => filter switch
    {
        1 => bytes < 1_000_000L,
        2 => bytes is >= 1_000_000L and < 10_000_000L,
        3 => bytes is >= 10_000_000L and < 100_000_000L,
        4 => bytes >= 100_000_000L,
        _ => true,
    };
}
