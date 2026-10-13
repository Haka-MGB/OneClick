namespace OneClick.App.Pages;

internal static class Path
{
    public static string? GetPathRoot(string path) => System.IO.Path.GetPathRoot(path);

    public static string? GetFileName(string path) => System.IO.Path.GetFileName(path);

    public static string? GetDirectoryName(string path) => System.IO.Path.GetDirectoryName(path);

    public static string Combine(string path1, string path2) => System.IO.Path.Combine(path1, path2);
}
