using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OneClick.App.Pages;

/// <summary>
/// Shared layout helpers used by every page.
/// Each page is a UserControl; this static class provides factory helpers
/// so all pages share the same structural look without a base-class constraint.
/// </summary>
public static class PageLayout
{
    public static FrameworkElement PageHeader(string title, string subtitle)
    {
        var panel = new StackPanel { Margin = new Thickness(32, 26, 32, 14) };

        var h1 = new TextBlock
        {
            Text = title,
            Style = Application.Current.FindResource("Text.Title") as Style,
        };

        var h2 = new TextBlock
        {
            Text = subtitle,
            Style = Application.Current.FindResource("Text.Secondary") as Style,
            Margin = new Thickness(0, 3, 0, 0),
        };

        panel.Children.Add(h1);
        panel.Children.Add(h2);
        return panel;
    }

    public static Border Card(UIElement content, Thickness? margin = null)
    {
        var card = new Border
        {
            Style = Application.Current.FindResource("Card") as Style,
            Margin = margin ?? new Thickness(0, 0, 0, 14),
            Child = content,
        };
        return card;
    }

    public static Border Banner(string message, BannerKind kind = BannerKind.Info)
    {
        var styleKey = kind switch
        {
            BannerKind.Warning => "Banner.Warning",
            BannerKind.Error   => "Banner.Error",
            BannerKind.Success => "Banner.Success",
            _                  => "Banner.Info",
        };

        var text = new TextBlock
        {
            Text = message,
            Style = Application.Current.FindResource("Text.Body") as Style,
        };

        return new Border
        {
            Style = Application.Current.FindResource(styleKey) as Style,
            Margin = new Thickness(0, 0, 0, 14),
            Child = text,
        };
    }

    public static TextBlock EmptyState(string message)
    {
        return new TextBlock
        {
            Text = message,
            Style = Application.Current.FindResource("Text.Secondary") as Style,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
            Margin = new Thickness(32),
        };
    }
}

public enum BannerKind { Info, Warning, Error, Success }
