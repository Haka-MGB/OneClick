using System.Windows;
using System.Windows.Controls;

namespace OneClick.App.Pages;

/// <summary>
/// Placeholder shown for features that are planned but not yet implemented.
/// Hidden from nav once the real page ships (SRS UX-02: hide, never grey out).
/// </summary>
public sealed class ComingSoonPage : UserControl
{
    public ComingSoonPage(string featureName, string description, string targetRelease)
    {
        var root = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();

        outer.Children.Add(PageLayout.PageHeader(featureName, description));

        var inner = new StackPanel { Margin = new Thickness(32, 0, 32, 20) };

        inner.Children.Add(PageLayout.Banner(
            $"This feature is planned for release {targetRelease} and is not yet available.",
            BannerKind.Info));

        outer.Children.Add(inner);
        root.Content = outer;
        Content = root;
    }
}
