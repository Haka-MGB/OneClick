using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>
/// Smart Search page — FR-FIND-01 to FR-FIND-08.
/// Read-only: nothing on disk is changed from this page.
/// </summary>
public sealed class SearchPage : UserControl
{
    private readonly MainWindow   _shell;
    private readonly SearchService _svc;

    private TextBox      _queryBox    = null!;
    private ComboBox     _dateFilter  = null!;
    private ComboBox     _typeFilter  = null!;
    private ComboBox     _sizeFilter  = null!;
    private TextBlock    _countLabel  = null!;
    private StackPanel   _resultsList = null!;
    private Border       _emptyState  = null!;
    private Button       _searchBtn   = null!;
    private CancellationTokenSource? _cts;

    public SearchPage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<SearchService>();
        Build();
    }

    // ── Build ───────────────────────────────────────────────────────────────

    private void Build()
    {
        var root = new Grid();
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        root.Children.Add(PageLayout.PageHeader("Smart Search", "Find files by name, date, type, or size. Nothing is changed."));
        Grid.SetRow(root.Children[^1], 0);

        // Search bar + filters
        var filterCard = new Border
        {
            Style  = Application.Current.FindResource("Card") as Style,
            Margin = new Thickness(32, 0, 32, 0),
        };

        var filterStack = new StackPanel { Spacing = 10 };

        // Query row
        var queryRow = new Grid();
        queryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        queryRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _queryBox = new TextBox
        {
            FontFamily         = Application.Current.FindResource("Font.Primary") as System.Windows.Media.FontFamily,
            FontSize           = 14,
            Padding            = new Thickness(12, 8, 12, 8),
            BorderThickness    = new Thickness(1),
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        _queryBox.KeyDown += (_, e) => { if (e.Key == Key.Return) RunSearch(); };
        AutomationHelper.SetName(_queryBox, "Search query");

        _searchBtn = new Button
        {
            Content = "Search",
            Style   = Application.Current.FindResource("Button.Primary") as Style,
            Margin  = new Thickness(10, 0, 0, 0),
            Width   = 100,
        };
        _searchBtn.Click += (_, _) => RunSearch();

        Grid.SetColumn(_queryBox, 0);
        Grid.SetColumn(_searchBtn, 1);
        queryRow.Children.Add(_queryBox);
        queryRow.Children.Add(_searchBtn);
        filterStack.Children.Add(queryRow);

        // Filter row
        var filterRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };

        _dateFilter = MakeComboBox("Date", new[]
        {
            "Any time", "Today", "This week", "This month", "Last 30 days", "Last 90 days"
        });
        _typeFilter = MakeComboBox("Type", new[]
        {
            "All types", "Images", "Documents", "Spreadsheets", "Video", "Audio", "Archives", "Installers", "Code"
        });
        _sizeFilter = MakeComboBox("Size", new[]
        {
            "Any size", "Under 1 MB", "1 – 10 MB", "10 – 100 MB", "Over 100 MB"
        });

        filterRow.Children.Add(MakeFilterLabel("Date:")); filterRow.Children.Add(_dateFilter);
        filterRow.Children.Add(MakeFilterLabel("Type:")); filterRow.Children.Add(_typeFilter);
        filterRow.Children.Add(MakeFilterLabel("Size:")); filterRow.Children.Add(_sizeFilter);
        filterStack.Children.Add(filterRow);

        // Quick searches
        var quickRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        quickRow.Children.Add(MakeFilterLabel("Quick:"));
        foreach (var qs in new[] { "Saved this week", "Recent downloads", "Largest files", "Photos last month" })
        {
            var chip = qs;
            var b = new Button { Content = chip, Style = Application.Current.FindResource("Button.Secondary") as Style, Padding = new Thickness(12, 6, 12, 6), MinHeight = 0 };
            b.Click += (_, _) => ApplyQuickSearch(chip);
            quickRow.Children.Add(b);
        }
        filterStack.Children.Add(quickRow);

        filterCard.Child = filterStack;
        root.Children.Add(filterCard);
        Grid.SetRow(root.Children[^1], 1);

        // Results area
        var resultsScroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(32, 14, 32, 0) };
        var resultsOuter  = new StackPanel { Spacing = 0 };

        // Count label
        _countLabel = new TextBlock
        {
            Style  = Application.Current.FindResource("Text.Secondary") as Style,
            Margin = new Thickness(0, 0, 0, 10),
        };
        resultsOuter.Children.Add(_countLabel);

        // Empty state
        _emptyState = PageLayout.Card(
            PageLayout.EmptyState("Enter a search term to find files. Results appear here as they are found."));
        resultsOuter.Children.Add(_emptyState);

        _resultsList = new StackPanel { Spacing = 2 };
        resultsOuter.Children.Add(_resultsList);

        resultsScroll.Content = resultsOuter;
        root.Children.Add(resultsScroll);
        Grid.SetRow(root.Children[^1], 2);

        Content = root;
    }

    // ── Search logic ────────────────────────────────────────────────────────

    private void RunSearch()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _resultsList.Children.Clear();
        _emptyState.Visibility = Visibility.Collapsed;
        _countLabel.Text = "Searching…";

        var options = new SearchOptions
        {
            Query      = _queryBox.Text.Trim(),
            DateFilter = _dateFilter.SelectedIndex,
            TypeFilter = _typeFilter.SelectedItem?.ToString() ?? "All types",
            SizeFilter = _sizeFilter.SelectedIndex,
        };

        var ct = _cts.Token;

        Task.Run(async () =>
        {
            var count = 0;
            await foreach (var result in _svc.SearchAsync(options, ct))
            {
                if (ct.IsCancellationRequested) break;
                count++;
                var r = result; // capture
                var c = count;
                Dispatcher.InvokeAsync(() =>
                {
                    _resultsList.Children.Add(BuildResultRow(r));
                    _countLabel.Text = $"{c} file{(c == 1 ? "" : "s")} found";
                });
            }

            if (!ct.IsCancellationRequested)
            {
                Dispatcher.InvokeAsync(() =>
                {
                    if (count == 0)
                    {
                        _countLabel.Text = "No files found. Try a different term or wider date range.";
                        _emptyState.Visibility = Visibility.Visible;
                    }
                });
            }
        }, ct);
    }

    private UIElement BuildResultRow(SearchResult r)
    {
        var row = new Border
        {
            Background      = Application.Current.FindResource("Brush.Surface.Card") as System.Windows.Media.Brush,
            BorderBrush     = Application.Current.FindResource("Brush.Border") as System.Windows.Media.Brush,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(14, 10, 14, 10),
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Name + path
        var info = new StackPanel();
        info.Children.Add(new TextBlock
        {
            Text   = r.FileName,
            Style  = Application.Current.FindResource("Text.Body") as Style,
            FontWeight = FontWeights.SemiBold,
        });
        info.Children.Add(new TextBlock
        {
            Text       = r.Directory,
            Style      = Application.Current.FindResource("Text.Caption") as Style,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });

        // Size + date
        var meta = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(20, 0, 0, 0) };
        meta.Children.Add(new TextBlock { Text = HumanSize(r.Size), Style = Application.Current.FindResource("Text.Caption") as Style });
        meta.Children.Add(new TextBlock { Text = r.Modified.ToString("d MMM yyyy"), Style = Application.Current.FindResource("Text.Caption") as Style });

        // Actions
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        var showBtn = new Button { Content = "Show in Explorer", Style = Application.Current.FindResource("Button.Secondary") as Style, Padding = new Thickness(10, 4, 10, 4), MinHeight = 0, Margin = new Thickness(0, 0, 6, 0) };
        showBtn.Click += (_, _) => OpenFolder(r.FullPath);

        var renameBtn = new Button { Content = "Send to Rename", Style = Application.Current.FindResource("Button.Secondary") as Style, Padding = new Thickness(10, 4, 10, 4), MinHeight = 0 };
        renameBtn.Click += (_, _) => _shell.Navigate("Rename");

        actions.Children.Add(showBtn);
        actions.Children.Add(renameBtn);

        Grid.SetColumn(info, 0);
        Grid.SetColumn(meta, 1);
        Grid.SetColumn(actions, 2);
        grid.Children.Add(info);
        grid.Children.Add(meta);
        grid.Children.Add(actions);

        row.Child = grid;
        return row;
    }

    private void ApplyQuickSearch(string preset)
    {
        switch (preset)
        {
            case "Saved this week":
                _queryBox.Text = "";
                _dateFilter.SelectedIndex = 2; // This week
                break;
            case "Recent downloads":
                _queryBox.Text = "";
                _dateFilter.SelectedIndex = 5; // Last 90 days
                _typeFilter.SelectedIndex = 0;
                break;
            case "Largest files":
                _queryBox.Text = "";
                _sizeFilter.SelectedIndex = 4; // Over 100 MB
                break;
            case "Photos last month":
                _queryBox.Text = "";
                _dateFilter.SelectedIndex = 4; // Last 30 days
                _typeFilter.SelectedIndex = 1; // Images
                break;
        }
        RunSearch();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static ComboBox MakeComboBox(string name, string[] items)
    {
        var cb = new ComboBox { Width = 140, Height = 34 };
        foreach (var item in items) cb.Items.Add(item);
        cb.SelectedIndex = 0;
        AutomationHelper.SetName(cb, name);
        return cb;
    }

    private static TextBlock MakeFilterLabel(string text) =>
        new() { Text = text, Style = Application.Current.FindResource("Text.Secondary") as Style, VerticalAlignment = VerticalAlignment.Center };

    private static void OpenFolder(string filePath)
    {
        try
        {
            var dir = Path.GetDirectoryName(filePath);
            if (dir is not null)
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        }
        catch { /* Fail silently — Explorer might not be available */ }
    }

    private static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double n = bytes; int i = 0;
        while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; }
        return i == 0 ? $"{n:F0} B" : $"{n:F1} {units[i]}";
    }
}
