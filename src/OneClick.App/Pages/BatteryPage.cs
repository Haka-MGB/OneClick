using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>
/// Battery Report page — FR-BAT-01 to FR-BAT-05.
/// Read-only. Only visible on devices that have a battery.
/// </summary>
public sealed class BatteryPage : UserControl
{
    private readonly MainWindow      _shell;
    private readonly BatteryService  _svc;

    public BatteryPage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<BatteryService>();
        Build();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Battery",
            "Your battery's health and tips to make it last longer."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        var report = _svc.GetReport();

        // Health card
        content.Children.Add(BuildHealthCard(report));

        // Tips card
        content.Children.Add(BuildTipsCard());

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    private Border BuildHealthCard(BatteryReport report)
    {
        var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var stack = new StackPanel { Spacing = 12 };

        stack.Children.Add(new TextBlock
        {
            Text  = "Battery health",
            Style = Application.Current.FindResource("Text.Heading") as Style,
        });

        if (!report.IsAvailable)
        {
            stack.Children.Add(PageLayout.Banner(
                "Battery information is not available on this device.", BannerKind.Info));
            card.Child = stack;
            return card;
        }

        // Health percentage
        var healthPct = report.HealthPercent;
        var verdict   = healthPct switch
        {
            >= 80 => ("Good",    "#1E7A4C", BannerKind.Success),
            >= 60 => ("Reduced", "#B45309", BannerKind.Warning),
            _     => ("Worn — consider a replacement battery", "#B42318", BannerKind.Error),
        };

        var heroRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        heroRow.Children.Add(new TextBlock
        {
            Text       = $"{healthPct:F0}%",
            FontSize   = 40,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(verdict.Item2)),
            VerticalAlignment = VerticalAlignment.Center,
        });
        heroRow.Children.Add(new TextBlock
        {
            Text              = verdict.Item1,
            Style             = Application.Current.FindResource("Text.Heading") as Style,
            VerticalAlignment = VerticalAlignment.Center,
        });
        stack.Children.Add(heroRow);

        // Key-value rows
        stack.Children.Add(KeyValueRow("Design capacity",       report.DesignCapacityMwh.HasValue ? $"{report.DesignCapacityMwh:N0} mWh" : "Your device does not report this"));
        stack.Children.Add(KeyValueRow("Full-charge capacity",  report.FullChargeCapacityMwh.HasValue ? $"{report.FullChargeCapacityMwh:N0} mWh" : "Your device does not report this"));
        stack.Children.Add(KeyValueRow("Charge cycles",         report.CycleCount.HasValue ? $"{report.CycleCount}" : "Your device does not report this"));

        card.Child = stack;
        return card;
    }

    private Border BuildTipsCard()
    {
        var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var stack = new StackPanel { Spacing = 10 };

        stack.Children.Add(new TextBlock
        {
            Text  = "Tips to extend battery life",
            Style = Application.Current.FindResource("Text.Heading") as Style,
        });

        var tips = new[]
        {
            ("Lower screen brightness",  "The screen is the biggest battery drain. Reduce brightness when indoors.", "ms-settings:display"),
            ("Turn on Battery Saver",    "Battery Saver limits background activity when you need to stretch charge.",  "ms-settings:batterysaver"),
            ("Check apps using battery", "Some apps use power in the background. Review them and turn off what you do not need.", "ms-settings:batterysaver-usagedetails"),
            ("Turn off Bluetooth when not needed", "Bluetooth uses power continuously when on.", "ms-settings:bluetooth"),
        };

        foreach (var (title, detail, uri) in tips)
        {
            var tip = new Border
            {
                Style           = Application.Current.FindResource("Banner.Info") as Style,
                Margin          = new Thickness(0),
            };
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = title,  Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold });
            info.Children.Add(new TextBlock { Text = detail, Style = Application.Current.FindResource("Text.Secondary") as Style });
            Grid.SetColumn(info, 0);
            row.Children.Add(info);

            var u = uri;
            var openBtn = new Button
            {
                Content = "Open settings",
                Style   = Application.Current.FindResource("Button.Secondary") as Style,
                Padding = new Thickness(10, 4, 10, 4),
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Margin  = new Thickness(12, 0, 0, 0),
            };
            openBtn.Click += (_, _) => TryOpenUri(u);
            Grid.SetColumn(openBtn, 1);
            row.Children.Add(openBtn);

            tip.Child = row;
            stack.Children.Add(tip);
        }

        card.Child = stack;
        return card;
    }

    private static UIElement KeyValueRow(string key, string value)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var k = new TextBlock { Text = key,   Style = Application.Current.FindResource("Text.Secondary") as Style };
        var v = new TextBlock { Text = value, Style = Application.Current.FindResource("Text.Body") as Style };

        Grid.SetColumn(k, 0); Grid.SetColumn(v, 1);
        row.Children.Add(k); row.Children.Add(v);
        return row;
    }

    private static void TryOpenUri(string uri)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch { /* fallback: show instructions in the detail text */ }
    }
}
