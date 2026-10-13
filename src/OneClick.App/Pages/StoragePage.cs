using System.IO;
using System.Windows;
using System.Windows.Controls;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>Storage Analyser page — FR-STO-01 to FR-STO-08.</summary>
public sealed class StoragePage : UserControl
{
    private readonly MainWindow     _shell;
    private readonly StorageService _svc;

    private TextBox    _folderBox  = null!;
    private ComboBox   _sizeFilter = null!;
    private TextBlock  _scanInfo   = null!;
    private StackPanel _fileList   = null!;
    private Button     _scanBtn    = null!;
    private Button     _holdBtn    = null!;
    private CancellationTokenSource? _cts;
    private readonly List<ScanEntry> _results = [];

    public StoragePage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<StorageService>();
        Build();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Storage Analyser",
            "Find the largest files. Selected files go to a holding area first, so nothing is lost by mistake."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        // Controls card
        var ctrlCard  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var ctrlStack = new StackPanel { Spacing = 10 };

        var topRow = new Grid();
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        topRow.Children.Add(new TextBlock { Text = "Folder", Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });

        _folderBox = new TextBox { Padding = new Thickness(8, 6, 8, 6) };
        Grid.SetColumn(_folderBox, 1);
        topRow.Children.Add(_folderBox);

        var browse = new Button { Content = "Browse", Style = Application.Current.FindResource("Button.Secondary") as Style, Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog();
            if (dlg.ShowDialog() == true)
                _folderBox.Text = dlg.FolderName;
        };
        Grid.SetColumn(browse, 2);
        topRow.Children.Add(browse);
        ctrlStack.Children.Add(topRow);

        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        filterRow.Children.Add(new TextBlock { Text = "Show files larger than", Style = Application.Current.FindResource("Text.Body") as Style, VerticalAlignment = VerticalAlignment.Center });
        _sizeFilter = new ComboBox { Width = 140, Height = 34 };
        foreach (var item in new[] { "Any size", "10 MB", "100 MB", "500 MB" }) _sizeFilter.Items.Add(item);
        _sizeFilter.SelectedIndex = 1;
        filterRow.Children.Add(_sizeFilter);

        _scanBtn = new Button { Content = "Scan folder", Style = Application.Current.FindResource("Button.Primary") as Style };
        _scanBtn.Click += (_, _) => Scan();
        filterRow.Children.Add(_scanBtn);

        _scanInfo = new TextBlock { Style = Application.Current.FindResource("Text.Secondary") as Style, VerticalAlignment = VerticalAlignment.Center };
        filterRow.Children.Add(_scanInfo);
        ctrlStack.Children.Add(filterRow);

        ctrlCard.Child = ctrlStack;
        content.Children.Add(ctrlCard);

        // Results card
        var resultsCard  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var resultsStack = new StackPanel { Spacing = 8 };

        var resultsScroll = new ScrollViewer { MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _fileList = new StackPanel { Spacing = 2 };
        _fileList.Children.Add(PageLayout.EmptyState("Scan a folder to see the largest files."));
        resultsScroll.Content = _fileList;
        resultsStack.Children.Add(resultsScroll);

        var actionRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        _holdBtn = new Button { Content = "Move selected to holding area", Style = Application.Current.FindResource("Button.Primary") as Style, IsEnabled = false };
        _holdBtn.Click += (_, _) => MoveToHolding();
        actionRow.Children.Add(_holdBtn);

        var emptyHoldBtn = new Button { Content = "Empty holding area", Style = Application.Current.FindResource("Button.Danger") as Style };
        emptyHoldBtn.Click += (_, _) => EmptyHolding();
        actionRow.Children.Add(emptyHoldBtn);

        resultsStack.Children.Add(actionRow);
        resultsCard.Child = resultsStack;
        content.Children.Add(resultsCard);

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    private void Scan()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _results.Clear();
        _fileList.Children.Clear();
        _scanInfo.Text = "Scanning…";
        _scanBtn.IsEnabled = false;
        _holdBtn.IsEnabled = false;

        var folder  = _folderBox.Text.Trim();
        if (!Directory.Exists(folder)) { _scanInfo.Text = "Choose a valid folder first."; _scanBtn.IsEnabled = true; return; }

        var minBytes = _sizeFilter.SelectedIndex switch { 1 => 10L * 1024 * 1024, 2 => 100L * 1024 * 1024, 3 => 500L * 1024 * 1024, _ => 0L };
        var ct = _cts.Token;

        Task.Run(async () =>
        {
            await foreach (var entry in _svc.ScanAsync(folder, minBytes, ct))
            {
                if (ct.IsCancellationRequested) break;
                _results.Add(entry);
                var e = entry; var c = _results.Count;
                Dispatcher.InvokeAsync(() =>
                {
                    AddResultRow(e);
                    _scanInfo.Text = $"{c} files found, {HumanSize(_results.Sum(x => x.Size))} total";
                    _holdBtn.IsEnabled = true;
                });
            }
            Dispatcher.InvokeAsync(() => { _scanBtn.IsEnabled = true; });
        }, ct);
    }

    private readonly Dictionary<string, CheckBox> _checkboxes = [];

    private void AddResultRow(ScanEntry entry)
    {
        var cb  = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
        _checkboxes[entry.FullPath] = cb;

        var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(cb);

        var info = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
        info.Children.Add(new TextBlock { Text = Path.GetFileName(entry.FullPath), Style = Application.Current.FindResource("Text.Body") as Style });
        info.Children.Add(new TextBlock { Text = Path.GetDirectoryName(entry.FullPath) ?? "", Style = Application.Current.FindResource("Text.Caption") as Style, TextTrimming = TextTrimming.CharacterEllipsis });
        Grid.SetColumn(info, 1); row.Children.Add(info);

        var size = new TextBlock { Text = HumanSize(entry.Size), Style = Application.Current.FindResource("Text.Caption") as Style, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(20, 0, 10, 0) };
        Grid.SetColumn(size, 2); row.Children.Add(size);

        var showBtn = new Button { Content = "Show", Style = Application.Current.FindResource("Button.Secondary") as Style, Padding = new Thickness(8, 4, 8, 4), MinHeight = 0 };
        var path = entry.FullPath;
        showBtn.Click += (_, _) => { try { System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\""); } catch { } };
        Grid.SetColumn(showBtn, 3); row.Children.Add(showBtn);

        _fileList.Children.Add(row);
    }

    private void MoveToHolding()
    {
        var selected = _checkboxes.Where(kv => kv.Value.IsChecked == true).Select(kv => kv.Key).ToList();
        if (selected.Count == 0) { MessageBox.Show("Select one or more files first.", "One Click"); return; }

        var result = _svc.MoveToHolding(selected);
        _shell.SetStatus($"Moved {result.Succeeded} files to holding area.");
        _shell.SyncUndo("Storage cleanup");

        foreach (var path in selected) _checkboxes.Remove(path);
        Scan();
    }

    private void EmptyHolding()
    {
        var size = _svc.HoldingAreaSize();
        if (size == 0) { MessageBox.Show("The holding area is empty.", "One Click"); return; }

        if (MessageBox.Show($"Permanently delete {HumanSize(size)} from the holding area?\nThis cannot be undone.",
            "One Click", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            _svc.EmptyHolding();
            _shell.SetStatus($"Holding area emptied.");
        }
    }

    private static string HumanSize(long bytes)
    {
        string[] u = { "B", "KB", "MB", "GB", "TB" }; double n = bytes; int i = 0;
        while (n >= 1024 && i < u.Length - 1) { n /= 1024; i++; }
        return i == 0 ? $"{n:F0} B" : $"{n:F1} {u[i]}";
    }
}
