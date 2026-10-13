using System.Windows;
using System.Windows.Controls;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>Settings page — FR-SET-01 to FR-SET-04.</summary>
public sealed class SettingsPage : UserControl
{
    private readonly MainWindow       _shell;
    private readonly SettingsService  _svc;

    public SettingsPage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<SettingsService>();
        Build();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Settings", "Adjust One Click to suit how you work."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        content.Children.Add(BuildSection("Appearance", new[]
        {
            BuildComboSetting("Theme", new[] { "System default", "Light", "Dark" },
                _svc.Get("Theme", "System default"),
                v => { _svc.Set("Theme", v); _shell.SetStatus("Restart the app to apply the theme change."); }),
        }));

        content.Children.Add(BuildSection("Holding area", new[]
        {
            BuildTextSetting("Retention (days)", _svc.Get("HoldingRetentionDays", "30"),
                v => _svc.Set("HoldingRetentionDays", v)),
            BuildTextSetting("Custom holding folder (leave blank for default)", _svc.Get("HoldingFolder", ""),
                v => _svc.Set("HoldingFolder", v)),
        }));

        content.Children.Add(BuildSection("History", new[]
        {
            BuildTextSetting("Keep history for (days)", _svc.Get("HistoryRetentionDays", "90"),
                v => _svc.Set("HistoryRetentionDays", v)),
        }));

        content.Children.Add(BuildSection("Update check", new[]
        {
            BuildToggleSetting("Check for updates at startup", _svc.Get("UpdateCheck", "true") == "true",
                v => _svc.Set("UpdateCheck", v.ToString().ToLower())),
        }));

        content.Children.Add(BuildSection("Privacy", new[]
        {
            BuildToggleSetting("Send anonymous crash reports (opt-in)", _svc.Get("CrashReports", "false") == "true",
                v => _svc.Set("CrashReports", v.ToString().ToLower())),
        }));

        // About
        var aboutCard  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var aboutStack = new StackPanel { Spacing = 8 };
        aboutStack.Children.Add(new TextBlock { Text = "About", Style = Application.Current.FindResource("Text.Heading") as Style });
        aboutStack.Children.Add(new TextBlock { Text = "One Click — Version 1.0.0", Style = Application.Current.FindResource("Text.Body") as Style });
        aboutStack.Children.Add(new TextBlock { Text = "A safe, simple Windows utility toolkit.", Style = Application.Current.FindResource("Text.Secondary") as Style });

        var exportLogBtn = new Button { Content = "Export diagnostic log", Style = Application.Current.FindResource("Button.Secondary") as Style, HorizontalAlignment = HorizontalAlignment.Left };
        exportLogBtn.Click += (_, _) => ExportLog();
        aboutStack.Children.Add(exportLogBtn);

        var resetBtn = new Button { Content = "Reset all settings to defaults", Style = Application.Current.FindResource("Button.Danger") as Style, HorizontalAlignment = HorizontalAlignment.Left };
        resetBtn.Click += (_, _) =>
        {
            if (MessageBox.Show("Reset all settings to defaults?", "One Click", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _svc.ResetDefaults();
                _shell.SetStatus("Settings reset. Restart the app for all changes to take effect.");
            }
        };
        aboutStack.Children.Add(resetBtn);
        aboutCard.Child = aboutStack;
        content.Children.Add(aboutCard);

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    private Border BuildSection(string title, UIElement[] rows)
    {
        var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(new TextBlock { Text = title, Style = Application.Current.FindResource("Text.Heading") as Style });
        foreach (var row in rows) stack.Children.Add(row);
        card.Child = stack;
        return card;
    }

    private static UIElement BuildComboSetting(string label, string[] options, string current, Action<string> onChange)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        row.Children.Add(new TextBlock { Text = label, Style = Application.Current.FindResource("Text.Body") as Style, VerticalAlignment = VerticalAlignment.Center });
        var cb = new ComboBox { Width = 200, Height = 34 };
        foreach (var o in options) cb.Items.Add(o);
        cb.SelectedItem = current;
        if (cb.SelectedItem is null) cb.SelectedIndex = 0;
        cb.SelectionChanged += (_, _) => { if (cb.SelectedItem is string s) onChange(s); };
        Grid.SetColumn(cb, 1); row.Children.Add(cb);
        return row;
    }

    private static UIElement BuildTextSetting(string label, string current, Action<string> onChange)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
        row.Children.Add(new TextBlock { Text = label, Style = Application.Current.FindResource("Text.Body") as Style, VerticalAlignment = VerticalAlignment.Center });
        var tb = new TextBox { Width = 200, Text = current, Padding = new Thickness(8, 6, 8, 6) };
        tb.LostFocus += (_, _) => onChange(tb.Text.Trim());
        Grid.SetColumn(tb, 1); row.Children.Add(tb);
        return row;
    }

    private static UIElement BuildToggleSetting(string label, bool current, Action<bool> onChange)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock { Text = label, Style = Application.Current.FindResource("Text.Body") as Style, VerticalAlignment = VerticalAlignment.Center });
        var cb = new CheckBox { IsChecked = current, VerticalAlignment = VerticalAlignment.Center };
        cb.Checked   += (_, _) => onChange(true);
        cb.Unchecked += (_, _) => onChange(false);
        Grid.SetColumn(cb, 1); row.Children.Add(cb);
        return row;
    }

    private void ExportLog()
    {
        try
        {
            var dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"OneClick-Log-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            _svc.ExportLog(dest);
            _shell.SetStatus($"Log exported to {dest}");
        }
        catch (Exception ex) { MessageBox.Show($"Export failed: {ex.Message}", "One Click"); }
    }
}
