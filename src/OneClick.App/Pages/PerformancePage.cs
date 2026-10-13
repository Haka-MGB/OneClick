using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>
/// Performance Snapshot + Slow-Computer Wizard — FR-PERF-01 to FR-PERF-09.
/// Read-only except for the user-initiated "close this program" action.
/// </summary>
public sealed class PerformancePage : UserControl
{
    private readonly MainWindow          _shell;
    private readonly PerformanceService  _svc;
    private readonly DispatcherTimer     _timer;

    // Snapshot tab
    private TextBlock _cpuLabel   = null!;
    private TextBlock _memLabel   = null!;
    private TextBlock _diskLabel  = null!;
    private ProgressBar _cpuBar  = null!;
    private ProgressBar _memBar  = null!;
    private ProgressBar _diskBar = null!;
    private StackPanel  _processList = null!;

    // Wizard tab
    private StackPanel  _wizardPanel = null!;
    private int         _wizardStep  = 0;

    public PerformancePage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<PerformanceService>();
        Build();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => RefreshSnapshot();

        IsVisibleChanged += (_, e) =>
        {
            if ((bool)e.NewValue) { _timer.Start(); RefreshSnapshot(); }
            else _timer.Stop();
        };
    }

    // ── Build ───────────────────────────────────────────────────────────────

    private void Build()
    {
        var root = new StackPanel();
        root.Children.Add(PageLayout.PageHeader("Performance",
            "See what is using your computer's resources and get help if it is running slowly."));

        var tabs = new TabControl { Margin = new Thickness(32, 0, 32, 32), Background = Brushes.Transparent };

        tabs.Items.Add(BuildSnapshotTab());
        tabs.Items.Add(BuildWizardTab());

        root.Children.Add(tabs);
        Content = new ScrollViewer { Content = root, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    }

    // ── Snapshot Tab ────────────────────────────────────────────────────────

    private TabItem BuildSnapshotTab()
    {
        var panel = new StackPanel { Spacing = 14, Margin = new Thickness(0, 14, 0, 0) };

        // Gauges
        var gaugeCard = new Border { Style = Application.Current.FindResource("Card") as Style };
        var gaugeGrid = new Grid();
        for (int i = 0; i < 3; i++)
            gaugeGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        (_cpuLabel,  _cpuBar)  = AddGauge(gaugeGrid, 0, "Processor",  "CPU usage", "#2F6FED");
        (_memLabel,  _memBar)  = AddGauge(gaugeGrid, 1, "Memory",      "RAM in use", "#7C3AED");
        (_diskLabel, _diskBar) = AddGauge(gaugeGrid, 2, "Disk",        "Disk activity", "#059669");

        gaugeCard.Child = gaugeGrid;
        panel.Children.Add(gaugeCard);

        // Education tip
        panel.Children.Add(PageLayout.Banner(
            "Memory (RAM) is what your computer uses while programs are open. " +
            "Storage is your files. The processor runs everything. " +
            "High memory use can slow the whole computer.",
            BannerKind.Info));

        // Process list
        var processCard = new Border { Style = Application.Current.FindResource("Card") as Style };
        var processOuter = new StackPanel();
        processOuter.Children.Add(new TextBlock
        {
            Text   = "Top programs by memory use",
            Style  = Application.Current.FindResource("Text.Heading") as Style,
            Margin = new Thickness(0, 0, 0, 12),
        });
        _processList = new StackPanel { Spacing = 4 };
        processOuter.Children.Add(_processList);
        processCard.Child = processOuter;
        panel.Children.Add(processCard);

        return new TabItem { Header = "Snapshot", Content = panel };
    }

    private static (TextBlock label, ProgressBar bar) AddGauge(Grid grid, int col, string title, string subtitle, string color)
    {
        var stack = new StackPanel { Margin = new Thickness(0, 0, col < 2 ? 16 : 0, 0) };

        stack.Children.Add(new TextBlock { Text = title,    Style = Application.Current.FindResource("Text.Heading") as Style });
        stack.Children.Add(new TextBlock { Text = subtitle, Style = Application.Current.FindResource("Text.Caption") as Style, Margin = new Thickness(0, 2, 0, 8) });

        var label = new TextBlock { Text = "–", FontSize = 22, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) };
        stack.Children.Add(label);

        var bar = new ProgressBar { Height = 8, Margin = new Thickness(0, 6, 0, 0), Maximum = 100, Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)) };
        stack.Children.Add(bar);

        Grid.SetColumn(stack, col);
        grid.Children.Add(stack);
        return (label, bar);
    }

    // ── Wizard Tab ──────────────────────────────────────────────────────────

    private TabItem BuildWizardTab()
    {
        _wizardPanel = new StackPanel { Spacing = 14, Margin = new Thickness(0, 14, 0, 0) };
        StartWizard();
        return new TabItem { Header = "Slow-Computer Wizard", Content = _wizardPanel };
    }

    private static readonly (string Question, string[] Options)[] WizardQuestions =
    {
        ("When does the computer feel slow?",
            new[] { "All the time", "When starting up", "When I open many programs", "After a while" }),
        ("How long since you last restarted it?",
            new[] { "Today", "A few days ago", "Over a week ago", "I never restart it" }),
        ("Do programs take a long time to open?",
            new[] { "Yes, always", "Sometimes", "No" }),
        ("Do you get messages about low storage space?",
            new[] { "Yes, often", "Occasionally", "Never" }),
        ("How many programs appear in your system tray (bottom-right corner)?",
            new[] { "Fewer than 5", "5 to 10", "More than 10", "Not sure" }),
    };

    private readonly string[] _wizardAnswers = new string[5];

    private void StartWizard()
    {
        _wizardStep = 0;
        _wizardPanel.Children.Clear();
        _wizardPanel.Children.Add(new TextBlock
        {
            Text   = "Answer a few quick questions and the wizard will tell you what to try first.",
            Style  = Application.Current.FindResource("Text.Secondary") as Style,
        });
        ShowWizardStep();
    }

    private void ShowWizardStep()
    {
        // Remove previous question rows (keep intro text)
        while (_wizardPanel.Children.Count > 1)
            _wizardPanel.Children.RemoveAt(_wizardPanel.Children.Count - 1);

        if (_wizardStep >= WizardQuestions.Length)
        {
            ShowWizardResults();
            return;
        }

        var (question, options) = WizardQuestions[_wizardStep];
        var step = _wizardStep;

        _wizardPanel.Children.Add(new TextBlock
        {
            Text   = $"Question {step + 1} of {WizardQuestions.Length}: {question}",
            Style  = Application.Current.FindResource("Text.Heading") as Style,
        });

        foreach (var opt in options)
        {
            var o = opt;
            var btn = new Button
            {
                Content             = o,
                Style               = Application.Current.FindResource("Button.Secondary") as Style,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin              = new Thickness(0, 0, 0, 4),
            };
            btn.Click += (_, _) => { _wizardAnswers[step] = o; _wizardStep++; ShowWizardStep(); };
            _wizardPanel.Children.Add(btn);
        }
    }

    private void ShowWizardResults()
    {
        var snapshot = _svc.GetSnapshot();
        var recs = BuildRecommendations(snapshot);

        _wizardPanel.Children.Add(new TextBlock
        {
            Text   = recs.Count == 0 ? "No problems found." : "Here is what to try, most helpful first:",
            Style  = Application.Current.FindResource("Text.Heading") as Style,
        });

        if (recs.Count == 0)
        {
            _wizardPanel.Children.Add(PageLayout.Banner(
                "Your computer looks healthy. If it still feels slow, a restart often helps. " +
                "If not, try the Storage Analyser to check for full drives.", BannerKind.Success));
        }
        else
        {
            foreach (var (title, detail, navTarget) in recs)
            {
                var card = new Border { Style = Application.Current.FindResource("Card") as Style };
                var row  = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var info = new StackPanel();
                info.Children.Add(new TextBlock { Text = title,  Style = Application.Current.FindResource("Text.Heading") as Style });
                info.Children.Add(new TextBlock { Text = detail, Style = Application.Current.FindResource("Text.Secondary") as Style, Margin = new Thickness(0, 4, 0, 0) });

                Grid.SetColumn(info, 0);
                row.Children.Add(info);

                if (navTarget is not null)
                {
                    var nav = navTarget;
                    var goBtn = new Button
                    {
                        Content = "Open tool",
                        Style   = Application.Current.FindResource("Button.Primary") as Style,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin  = new Thickness(16, 0, 0, 0),
                    };
                    goBtn.Click += (_, _) => _shell.Navigate(nav);
                    Grid.SetColumn(goBtn, 1);
                    row.Children.Add(goBtn);
                }

                card.Child = row;
                _wizardPanel.Children.Add(card);
            }
        }

        // Export button (FR-PERF-09)
        var exportBtn = new Button
        {
            Content             = "Copy results to clipboard",
            Style               = Application.Current.FindResource("Button.Secondary") as Style,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        exportBtn.Click += (_, _) => ExportResults(snapshot, recs);
        _wizardPanel.Children.Add(exportBtn);

        var restartBtn = new Button
        {
            Content             = "Start again",
            Style               = Application.Current.FindResource("Button.Secondary") as Style,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        restartBtn.Click += (_, _) => StartWizard();
        _wizardPanel.Children.Add(restartBtn);
    }

    // ── Recommendations engine ──────────────────────────────────────────────

    private static List<(string Title, string Detail, string? NavTarget)> BuildRecommendations(PerformanceSnapshot s)
    {
        var list = new List<(string, string, string?)>();

        if (s.DiskUsedPercent >= 90)
            list.Add(("Drive is almost full",
                      $"Your drive is {s.DiskUsedPercent:F0}% full. A nearly full drive makes the computer much slower. Free up space with the Storage Analyser.",
                      "Storage"));

        if (s.MemoryUsedPercent >= 85)
            list.Add(("Memory is very high",
                      $"Memory is {s.MemoryUsedPercent:F0}% used. Close programs you are not using.",
                      null));

        if (s.StartupProgramCount >= 10)
            list.Add(($"{s.StartupProgramCount} programs start with Windows",
                      "Too many startup programs slow down sign-in and use memory. Review them in Startup Manager.",
                      "Startup"));

        if (s.DaysSinceLastRestart >= 7)
            list.Add(($"Last restart was {s.DaysSinceLastRestart} days ago",
                      "Restarting clears temporary files and applies updates. Try restarting the computer.",
                      null));

        return list;
    }

    private static void ExportResults(PerformanceSnapshot s, List<(string Title, string Detail, string?)> recs)
    {
        try
        {
            var text = $"One Click — Performance Results — {DateTime.Now:d MMM yyyy HH:mm}\n" +
                       $"CPU: {s.CpuPercent:F0}%  Memory: {s.MemoryUsedPercent:F0}%  Disk: {s.DiskUsedPercent:F0}%\n\n";
            text += recs.Count == 0
                ? "No problems found.\n"
                : string.Join("\n", recs.Select(r => $"• {r.Title}\n  {r.Detail}")) + "\n";

            Clipboard.SetText(text);
        }
        catch { /* best effort */ }
    }

    // ── Snapshot refresh ─────────────────────────────────────────────────────

    private void RefreshSnapshot()
    {
        var s = _svc.GetSnapshot();

        _cpuLabel.Text  = $"{s.CpuPercent:F0}%";
        _memLabel.Text  = $"{s.MemoryUsedPercent:F0}%";
        _diskLabel.Text = $"{s.DiskUsedPercent:F0}%";

        _cpuBar.Value  = s.CpuPercent;
        _memBar.Value  = s.MemoryUsedPercent;
        _diskBar.Value = s.DiskUsedPercent;

        _processList.Children.Clear();
        foreach (var proc in s.TopProcesses.Take(5))
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock { Text = proc.FriendlyName, Style = Application.Current.FindResource("Text.Body") as Style };
            var mem  = new TextBlock { Text = HumanSize(proc.MemoryBytes), Style = Application.Current.FindResource("Text.Caption") as Style, Margin = new Thickness(20, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };

            Grid.SetColumn(name, 0); Grid.SetColumn(mem, 1);
            row.Children.Add(name); row.Children.Add(mem);

            // Only user-started processes get a close button (FR-PERF-04 + FR-PERF-05)
            if (proc.CanClose)
            {
                var closeBtn = new Button
                {
                    Content = "Close",
                    Style   = Application.Current.FindResource("Button.Danger") as Style,
                    Padding = new Thickness(10, 4, 10, 4),
                    MinHeight = 0,
                    Margin  = new Thickness(10, 0, 0, 0),
                };
                var p = proc;
                closeBtn.Click += (_, _) => ConfirmClose(p);
                Grid.SetColumn(closeBtn, 2);
                row.Children.Add(closeBtn);
            }

            _processList.Children.Add(row);
        }
    }

    private void ConfirmClose(ProcessEntry proc)
    {
        var msg = $"Close {proc.FriendlyName}?\n\nUnsaved work in this program may be lost.";
        if (MessageBox.Show(msg, "One Click — Close program",
            MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
        {
            var closed = _svc.RequestClose(proc.ProcessId);
            _shell.SetStatus(closed
                ? $"{proc.FriendlyName} was asked to close."
                : $"{proc.FriendlyName} did not respond. Use Task Manager to force-close if needed.");
        }
    }

    private static string HumanSize(long bytes)
    {
        string[] units = { "B", "KB", "MB", "GB" };
        double n = bytes; int i = 0;
        while (n >= 1024 && i < units.Length - 1) { n /= 1024; i++; }
        return $"{n:F1} {units[i]}";
    }
}
