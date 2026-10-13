using System.Windows;

namespace OneClick.App.Pages;

public class StackPanel : System.Windows.Controls.StackPanel
{
    public static readonly DependencyProperty SpacingProperty =
        DependencyProperty.Register(
            nameof(Spacing),
            typeof(double),
            typeof(StackPanel),
            new PropertyMetadata(0d, OnSpacingChanged));

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override void OnVisualChildrenChanged(DependencyObject visualAdded, DependencyObject visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        ApplySpacing();
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StackPanel panel)
        {
            panel.ApplySpacing();
        }
    }

    private void ApplySpacing()
    {
        for (var i = 0; i < Children.Count; i++)
        {
            if (Children[i] is not FrameworkElement child)
            {
                continue;
            }

            var margin = child.Margin;
            if (Orientation == System.Windows.Controls.Orientation.Horizontal)
            {
                child.Margin = new Thickness(i == 0 ? margin.Left : Spacing, margin.Top, margin.Right, margin.Bottom);
            }
            else
            {
                child.Margin = new Thickness(margin.Left, i == 0 ? margin.Top : Spacing, margin.Right, margin.Bottom);
            }
        }
    }
}
