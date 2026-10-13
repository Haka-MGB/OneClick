using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using OneClick.App.Services;

namespace OneClick.App.Pages;

public sealed class OverviewPage : UserControl, IUndoTarget
{
    private readonly MainWindow _shell;
    private readonly JournalService _journal;
    private Border _diskCard = null!;
    private StackPanel _activityPanel = null!;

    public OverviewPage(MainWindow shell)
    {
        _shell   = shell;
        _journal = ServiceLocator.Get<JournalService>();
        Build();
        Refresh();
    }

    // ── Build ───────────────────────────────────────────────────────────────

    private void Build()
    {
        var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Overview", "The state of this computer and the tools available."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32) };

        // Disk card placeholder — populated in Refresh()
        _diskCard = PageLayout.Card(new TextBlock { Text = "Loading…" });
        content.Children.Add(_diskCard);

        // Tool cards row
        content.Children.Add(BuildToolCards());

        // Recent activity
        var activityCard = new Border { Style = Application.Current.FindResource("Card") as Style };
        var activityStack = new StackPanel();
        activityStack.Children.Add(new TextBlock
        {
            Text = "Recent activity",
            Style = Application.Current.FindResource("Text.Heading") as Style,
            Margin = new Thickness(0, 0, 0, 12),
        });
        _activityPanel = new StackPanel();
        activityStack.Children.Add(_activityPanel);
        activityCard.Child = activityStack;
        content.Children.Add(activityCard);

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    private UIElement BuildToolCards()
    {
        var items = new[]
        {
            ("Smart Search",      "Find files you saved but cannot locate.",             "Search"),
            ("Bulk Rename",       "Rename hundreds of files with simple rules.",         "Rename"),
            ("Folder Organiser",  "Sort a cluttered folder into tidy sub-folders.",      "Organise"),
            ("Storage Analyser",  "Find the largest files and move them safely.",        "Storage"),
            ("Performance",       "See what is slowing your computer and fix it.",       "Performance"),
            ("Automation",        "Set up rules that organise files automatically.",     "Automation"),
        };

        var wrap = new WrapPanel { ItemWidth = 220 };

        foreach (var (name, desc, nav) in items)
        {
            var card = new Border
            {
                Style  = Application.Current.FindResource("Card") as Style,
                Margin = new Thickness(0, 0, 14, 14),
                Width  = 206,
                Cursor = System.Windows.Input.Cursors.Hand,
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text   = name,
                Style  = Application.Current.FindResource("Text.Heading") as Style,
                Margin = new Thickness(0, 0, 0, 6),
            });
            stack.Children.Add(new TextBlock
            {
                Text        = desc,
                Style       = Application.Current.FindResource("Text.Secondary") as Style,
                Margin      = new Thickness(0, 0, 0, 14),
                TextWrapping = TextWrapping.Wrap,
            });

            var navKey = nav;
            var btn = new Button
            {
                Content = $"Open {name}",
                Style   = Application.Current.FindResource("Button.Primary") as Style,
                HorizontalAlignment = HorizontalAlignment.Left,
            };
            btn.Click += (_, _) => _shell.Navigate(navKey);

            stack.Children.Add(btn);
            card.Child = stack;
            wrap.Children.Add(card);
        }

        return wrap;
    }

    // ── Refresh ─────────────────────────────────────────────────────────────

    public void Refresh()
    {
        RefreshDisk();
        RefreshActivity();
        _shell.SyncUndo(_journal.LastActionLabel());
    }

    private void RefreshDisk()
    {
        try
        {
            var root  = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "C:\\";
            var info  = new DriveInfo(root);
            var used  = info.TotalSize - info.AvailableFreeSpace;
            var pct   = (double)used / info.TotalSize * 100;

            var stack = new StackPanel { Margin = new Thickness(0) };

            stack.Children.Add(new TextBlock
            {
                Text  = "Disk usage",
                Style = Application.Current.FindResource("Text.Heading") as Style,
                Margin = new Thickness(0, 0, 0, 4),
            });

            stack.Children.Add(new TextBlock
            {
                Text  = $"{HumanSize(used)} used of {HumanSize(info.TotalSize)} on {root}  ({pct:F0}% full)",
                Style = Application.Current.FindResource("Text.Secondary") as Style,
                Margin = new Thickness(0, 0, 0, 10),
            });

            // Progress bar
            var track = new Border { Height = 8, CornerRadius = new CornerRadius(4), Background = new SolidColorBrush(Color.FromRgb(0xE6, 0xEA, 0xF1)) };
            var fill  = new Border
            {
                Height       = 8,
                CornerRadius = new CornerRadius(4),
                Background   = new SolidColorBrush(pct >= 90 ? Color.FromRgb(0xC2, 0x41, 0x0C) : Color.FromRgb(0x2F, 0x6F, 0xED)),
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            // Bind width after layout
            track.SizeChanged += (_, e) => fill.Width = e.NewSize.Width * pct / 100;
            var progressHost = new Grid();
            progressHost.Children.Add(track);
            progressHost.Children.Add(fill);
            stack.Children.Add(progressHost);

            var msg = pct >= 90
                ? "This drive is almost full. A full drive slows the whole computer. Use Storage Analyser to free space."
                : pct >= 75
                    ? "Space is getting tight. Consider reviewing large files soon."
                    : "There is plenty of free space.";

            var bannerKind = pct >= 90 ? BannerKind.Warning : BannerKind.Info;

            stack.Children.Add(PageLayout.Banner(msg, bannerKind));

            _diskCard.Child = stack;
        }
        catch
        {
            _diskCard.Child = new TextBlock
            {
                Text  = "Disk information could not be loaded.",
                Style = Application.Current.FindResource("Text.Secondary") as Style,
            };
        }
    }

    private void RefreshActivity()
    {
        _activityPanel.Children.Clear();
        var items = _journal.RecentItems(6);

        if (items.Count == 0)
        {
            _activityPanel.Children.Add(PageLayout.EmptyState(
                "Nothing yet. Actions you apply will appear here and can be undone."));
            return;
        }

        foreach (var item in items)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text  = item.Label,
                Style = Application.Current.FindResource("Text.Body") as Style,
            };
            var meta = new TextBlock
            {
                Text  = $"{item.ItemCount} files   {item.CreatedAt:d MMM yyyy, HH:mm}",
                Style = Application.Current.FindResource("Text.Caption") as Style,
            };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(meta, 1);
            row.Children.Add(label);
            row.Children.Add(meta);
            _activityPanel.Children.Add(row);
        }
    }

    public void RequestUndo()
    {
        var result = _journal.UndoLast();
        if (result is not null)
        {
            _shell.SetStatus($"Undone: {result.Label} ({result.ItemCount} files restored).");
            Refresh();
        }
    }

    private static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double n = bytes;
        int i = 0;
        while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; }
        return i == 0 ? $"{n:F0} B" : $"{n:F1} {units[i]}";
    }
}
