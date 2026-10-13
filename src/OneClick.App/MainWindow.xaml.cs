using System.Windows;
using System.Windows.Controls;
using OneClick.App.Pages;

namespace OneClick.App;

public partial class MainWindow : Window
{
    // All nav buttons in display order
    private readonly Dictionary<string, Button> _navButtons;
    private readonly Dictionary<string, Func<UIElement>> _pageFactories;

    public MainWindow()
    {
        InitializeComponent();

        _navButtons = new Dictionary<string, Button>(StringComparer.Ordinal)
        {
            ["Overview"]    = NavOverview,
            ["Search"]      = NavSearch,
            ["Rename"]      = NavRename,
            ["Organise"]    = NavOrganise,
            ["Storage"]     = NavStorage,
            ["History"]     = NavHistory,
            ["Performance"] = NavPerformance,
            ["Duplicates"]  = NavDuplicates,
            ["Startup"]     = NavStartup,
            ["Automation"]  = NavAutomation,
            ["Guides"]      = NavGuides,
            ["Migration"]   = NavMigration,
            ["Battery"]     = NavBattery,
            ["Settings"]    = NavSettings,
        };

        // Lazy page factories — each page is constructed only when first navigated to
        _pageFactories = new Dictionary<string, Func<UIElement>>(StringComparer.Ordinal)
        {
            ["Overview"]    = () => new OverviewPage(this),
            ["Search"]      = () => new SearchPage(this),
            ["Rename"]      = () => new RenamePage(this),
            ["Organise"]    = () => new OrganisePage(this),
            ["Storage"]     = () => new StoragePage(this),
            ["History"]     = () => new HistoryPage(this),
            ["Performance"] = () => new PerformancePage(this),
            ["Duplicates"]  = () => new ComingSoonPage("Duplicates", "Find and safely remove duplicate files.", "R2"),
            ["Startup"]     = () => new ComingSoonPage("Startup Manager", "Control which programs launch with Windows.", "R2"),
            ["Automation"]  = () => new AutomationPage(this),
            ["Guides"]      = () => new ComingSoonPage("Guides", "Step-by-step guides for common tasks.", "R2"),
            ["Migration"]   = () => new MigrationPage(this),
            ["Battery"]     = () => new BatteryPage(this),
            ["Settings"]    = () => new SettingsPage(this),
        };

        // Show the Battery nav item only on devices that report a battery
        NavBattery.Visibility = BatteryHelper.DeviceHasBattery()
            ? Visibility.Visible
            : Visibility.Collapsed;

        Navigate("Overview");
    }

    // ── Public API used by pages ────────────────────────────────────────────

    public void Navigate(string pageKey)
    {
        // Deactivate all nav buttons
        foreach (var btn in _navButtons.Values)
            btn.Tag = null;

        // Activate the requested one
        if (_navButtons.TryGetValue(pageKey, out var active))
            active.Tag = "Active";

        // Swap the page
        if (_pageFactories.TryGetValue(pageKey, out var factory))
            PageHost.Content = factory();
    }

    public void SetStatus(string message)
    {
        TxtStatus.Text = message;
    }

    public void SyncUndo(string? lastActionLabel)
    {
        if (lastActionLabel is null)
        {
            TxtLastAction.Text = "No actions yet";
            BtnUndo.IsEnabled = false;
        }
        else
        {
            TxtLastAction.Text = $"Last: {lastActionLabel}";
            BtnUndo.IsEnabled = true;
        }
    }

    // ── Event handlers ──────────────────────────────────────────────────────

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn)
        {
            var key = _navButtons.FirstOrDefault(p => p.Value == btn).Key;
            if (key is not null)
                Navigate(key);
        }
    }

    private void BtnUndo_Click(object sender, RoutedEventArgs e)
    {
        // UndoService is resolved through the page; delegate to Overview which always exists
        if (PageHost.Content is IUndoTarget target)
            target.RequestUndo();
        else
            Navigate("Overview");
    }
}

/// <summary>Interface a page can implement to receive the global Undo button click.</summary>
public interface IUndoTarget
{
    void RequestUndo();
}
