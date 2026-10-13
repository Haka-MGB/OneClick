using System.IO;
using System.Windows;
using System.Windows.Controls;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>Folder Organiser page — FR-ORG-01 to FR-ORG-06.</summary>
public sealed class OrganisePage : UserControl
{
    private readonly MainWindow     _shell;
    private readonly OrganiseService _svc;

    private TextBox    _folderBox   = null!;
    private RadioButton _byType     = null!;
    private RadioButton _byMonth    = null!;
    private TextBlock   _previewCount = null!;
    private StackPanel  _previewList  = null!;
    private Button      _applyBtn     = null!;

    public OrganisePage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<OrganiseService>();
        Build();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Folder Organiser",
            "Sort loose files into tidy sub-folders. Nothing is deleted — every move can be undone."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        // Folder picker
        var folderCard = new Border { Style = Application.Current.FindResource("Card") as Style };
        var folderRow  = new Grid();
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        folderRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        folderRow.Children.Add(new TextBlock { Text = "Folder", Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });

        _folderBox = new TextBox { Padding = new Thickness(8, 6, 8, 6), VerticalContentAlignment = VerticalAlignment.Center };
        _folderBox.TextChanged += (_, _) => UpdatePreview();
        Grid.SetColumn(_folderBox, 1);
        folderRow.Children.Add(_folderBox);

        var browse = new Button { Content = "Browse", Style = Application.Current.FindResource("Button.Secondary") as Style, Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog();
            if (dlg.ShowDialog() == true)
                _folderBox.Text = dlg.FolderName;
        };
        Grid.SetColumn(browse, 2);
        folderRow.Children.Add(browse);
        folderCard.Child = folderRow;
        content.Children.Add(folderCard);

        // Mode + preview card
        var mainCard  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var mainStack = new StackPanel { Spacing = 12 };

        var modeRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 16 };
        modeRow.Children.Add(new TextBlock { Text = "Group files", Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center });
        _byType  = new RadioButton { Content = "By file type",             IsChecked = true };
        _byMonth = new RadioButton { Content = "By month last modified" };
        _byType.Checked  += (_, _) => UpdatePreview();
        _byMonth.Checked += (_, _) => UpdatePreview();
        modeRow.Children.Add(_byType);
        modeRow.Children.Add(_byMonth);
        mainStack.Children.Add(modeRow);

        _previewCount = new TextBlock { Text = "Choose a folder to begin.", Style = Application.Current.FindResource("Text.Secondary") as Style };
        mainStack.Children.Add(_previewCount);

        var scroll = new ScrollViewer { MaxHeight = 350, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _previewList = new StackPanel { Spacing = 2 };
        scroll.Content = _previewList;
        mainStack.Children.Add(scroll);

        _applyBtn = new Button
        {
            Content             = "Organise files",
            Style               = Application.Current.FindResource("Button.Primary") as Style,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled           = false,
        };
        _applyBtn.Click += (_, _) => Apply();
        mainStack.Children.Add(_applyBtn);

        mainCard.Child = mainStack;
        content.Children.Add(mainCard);

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    private void UpdatePreview()
    {
        _previewList.Children.Clear();
        _applyBtn.IsEnabled = false;
        var folder = _folderBox.Text.Trim();
        if (!Directory.Exists(folder)) { _previewCount.Text = "Choose a folder to begin."; return; }

        var byMonth = _byMonth.IsChecked == true;
        var plan    = _svc.Plan(folder, byMonth);
        _previewCount.Text = $"{plan.Count} files will be moved into sub-folders";
        _applyBtn.IsEnabled = plan.Count > 0;

        foreach (var item in plan.Items.Take(200))
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock { Text = item.FileName, Style = Application.Current.FindResource("Text.Caption") as Style, TextTrimming = TextTrimming.CharacterEllipsis });
            row.Children.Add(new TextBlock { Text = $"→ {item.SubFolder}", Style = Application.Current.FindResource("Text.Caption") as Style, Foreground = Application.Current.FindResource("Brush.Accent") as System.Windows.Media.Brush, Margin = new Thickness(20, 0, 0, 0) });
            Grid.SetColumn(row.Children[^1], 1);

            _previewList.Children.Add(row);
        }
    }

    private void Apply()
    {
        var folder  = _folderBox.Text.Trim();
        var byMonth = _byMonth.IsChecked == true;
        var plan    = _svc.Plan(folder, byMonth);
        if (plan.Count == 0) return;

        if (MessageBox.Show($"Move {plan.Count} files into sub-folders?\nYou can undo this afterwards.",
            "One Click — Folder Organiser", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var result = _svc.Apply(folder, byMonth);
        _shell.SetStatus($"Organised {result.Succeeded} files." + (result.Skipped > 0 ? $" {result.Skipped} skipped." : ""));
        _shell.SyncUndo("Folder organise");
        UpdatePreview();
    }
}
