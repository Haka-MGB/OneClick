using System.Windows;
using System.Windows.Automation;

namespace OneClick.App.Services;

/// <summary>Accessibility helpers — sets AutomationProperties for screen readers.</summary>
public static class AutomationHelper
{
    public static void SetName(DependencyObject element, string name)
        => AutomationProperties.SetName(element, name);

    public static void SetHelpText(DependencyObject element, string help)
        => AutomationProperties.SetHelpText(element, help);
}
