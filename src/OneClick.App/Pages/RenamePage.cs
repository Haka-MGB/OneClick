using System.IO;
using System.Windows;
using System.Windows.Controls;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>Bulk Rename page — FR-REN-01 to FR-REN-10.</summary>
public sealed class RenamePage : UserControl
{
    private readonly MainWindow    _shell;
    private readonly RenameService _svc;

    private TextBox    _folderBox  = null!;
    private TextBox    _findBox    = null!;
    private TextBox    _replaceBox = null!;
    private TextBox    _prefixBox  = null!;
    private TextBox    _suffixBox  = null!;
    private ComboBox   _caseBox    = null!;
    private CheckBox   _numberChk  = null!;
    private TextBox    _startBox   = null!;
    private TextBlock  _previewCount = null!;
    private StackPanel _previewList  = null!;
    private Button     _applyBtn     = null!;

    private CancellationTokenSource? _debounce;

    public RenamePage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<RenameService>();
        Build();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Bulk Rename",
            "Rename every file in a folder with simple rules. Check the preview, then apply. Undo is always available."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        // Folder picker
        content.Children.Add(BuildFolderCard());

        // Two-column layout: options | preview
        var cols = new Grid();
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
        cols.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        cols.Children.Add(BuildOptionsCard());
        Grid.SetColumn(cols.Children[^1], 0);

        cols.Children.Add(BuildPreviewCard());
        Grid.SetColumn(cols.Children[^1], 1);

        content.Children.Add(cols);
        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    private Border BuildFolderCard()
    {
        var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var row   = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        row.Children.Add(new TextBlock { Text = "Folder", Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) });

        _folderBox = new TextBox { Padding = new Thickness(8, 6, 8, 6), VerticalContentAlignment = VerticalAlignment.Center };
        _folderBox.TextChanged += (_, _) => SchedulePreview();
        Grid.SetColumn(_folderBox, 1);
        row.Children.Add(_folderBox);

        var browse = new Button { Content = "Browse", Style = Application.Current.FindResource("Button.Secondary") as Style, Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog();
            if (dlg.ShowDialog() == true)
                _folderBox.Text = dlg.FolderName;
        };
        Grid.SetColumn(browse, 2);
        row.Children.Add(browse);

        card.Child = row;
        return card;
    }

    private Border BuildOptionsCard()
    {
        var card  = new Border { Style = Application.Current.FindResource("Card") as Style, Margin = new Thickness(0, 0, 14, 0) };
        var stack = new StackPanel { Spacing = 10 };

        stack.Children.Add(new TextBlock { Text = "Rules", Style = Application.Current.FindResource("Text.Heading") as Style });

        _findBox    = AddLabeledBox(stack, "Find text");
        _replaceBox = AddLabeledBox(stack, "Replace with");
        _prefixBox  = AddLabeledBox(stack, "Add prefix");
        _suffixBox  = AddLabeledBox(stack, "Add suffix");

        stack.Children.Add(new TextBlock { Text = "Letter case", Style = Application.Current.FindResource("Text.Secondary") as Style });
        _caseBox = new ComboBox { Height = 34 };
        foreach (var item in new[] { "Keep as is", "lowercase", "UPPERCASE", "Title Case" }) _caseBox.Items.Add(item);
        _caseBox.SelectedIndex = 0;
        _caseBox.SelectionChanged += (_, _) => SchedulePreview();
        stack.Children.Add(_caseBox);

        _numberChk = new CheckBox { Content = "Add sequence number", Margin = new Thickness(0, 6, 0, 0) };
        _numberChk.Checked   += (_, _) => SchedulePreview();
        _numberChk.Unchecked += (_, _) => SchedulePreview();
        stack.Children.Add(_numberChk);

        _startBox = AddLabeledBox(stack, "Start at", "1");

        card.Child = stack;
        return card;
    }

    private Border BuildPreviewCard()
    {
        var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var stack = new StackPanel { Spacing = 10 };

        _previewCount = new TextBlock { Text = "Choose a folder to see a preview.", Style = Application.Current.FindResource("Text.Secondary") as Style };
        stack.Children.Add(_previewCount);

        var scroll = new ScrollViewer { MaxHeight = 400, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _previewList = new StackPanel { Spacing = 2 };
        scroll.Content = _previewList;
        stack.Children.Add(scroll);

        _applyBtn = new Button
        {
            Content             = "Apply rename",
            Style               = Application.Current.FindResource("Button.Primary") as Style,
            HorizontalAlignment = HorizontalAlignment.Right,
            IsEnabled           = false,
        };
        _applyBtn.Click += (_, _) => Apply();
        stack.Children.Add(_applyBtn);

        card.Child = stack;
        return card;
    }

    private TextBox AddLabeledBox(Panel parent, string label, string defaultVal = "")
    {
        parent.Children.Add(new TextBlock { Text = label, Style = Application.Current.FindResource("Text.Secondary") as Style });
        var box = new TextBox { Padding = new Thickness(8, 6, 8, 6), Text = defaultVal };
        box.TextChanged += (_, _) => SchedulePreview();
        parent.Children.Add(box);
        return box;
    }

    private void SchedulePreview()
    {
        _debounce?.Cancel();
        _debounce = new CancellationTokenSource();
        var ct = _debounce.Token;
        Task.Delay(250, ct).ContinueWith(_ =>
        {
            if (!ct.IsCancellationRequested)
                Dispatcher.InvokeAsync(UpdatePreview);
        }, TaskScheduler.Default);
    }

    private void UpdatePreview()
    {
        _previewList.Children.Clear();
        _applyBtn.IsEnabled = false;

        var folder = _folderBox.Text.Trim();
        if (!Directory.Exists(folder))
        {
            _previewCount.Text = "Choose a folder to begin.";
            return;
        }

        var opts = BuildOptions();
        var plan = _svc.Plan(folder, opts);

        _previewCount.Text = $"{plan.Count} of {plan.TotalFiles} files will change";
        _applyBtn.IsEnabled = plan.Count > 0;

        foreach (var item in plan.Items.Take(200))
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            row.Children.Add(new TextBlock { Text = item.OldName, Style = Application.Current.FindResource("Text.Caption") as Style, TextTrimming = TextTrimming.CharacterEllipsis });
            Grid.SetColumn(row.Children[^1], 0);

            row.Children.Add(new TextBlock { Text = " → ", Style = Application.Current.FindResource("Text.Secondary") as Style });
            Grid.SetColumn(row.Children[^1], 1);

            row.Children.Add(new TextBlock { Text = item.NewName, Style = Application.Current.FindResource("Text.Caption") as Style, TextTrimming = TextTrimming.CharacterEllipsis, FontWeight = FontWeights.SemiBold });
            Grid.SetColumn(row.Children[^1], 2);

            _previewList.Children.Add(row);
        }
    }

    private void Apply()
    {
        var plan = _svc.Plan(_folderBox.Text.Trim(), BuildOptions());
        if (plan.Count == 0) return;

        var sample = string.Join("\n", plan.Items.Take(5).Select(i => $"  {i.OldName} → {i.NewName}"));
        var more   = plan.Count > 5 ? $"\n  …and {plan.Count - 5} more." : "";
        if (MessageBox.Show($"Rename {plan.Count} files?\n\n{sample}{more}\n\nYou can undo this afterwards.",
            "One Click — Bulk Rename", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var result = _svc.Apply(_folderBox.Text.Trim(), BuildOptions());
        _shell.SetStatus($"Renamed {result.Succeeded} files." + (result.Skipped > 0 ? $" {result.Skipped} skipped." : ""));
        _shell.SyncUndo("Bulk rename");
        UpdatePreview();
    }

    private RenameOptions BuildOptions() => new()
    {
        Find    = _findBox.Text,
        Replace = _replaceBox.Text,
        Prefix  = _prefixBox.Text,
        Suffix  = _suffixBox.Text,
        CaseMode = _caseBox.SelectedIndex,
        AddNumber = _numberChk.IsChecked == true,
        NumberStart = int.TryParse(_startBox.Text, out var s) ? s : 1,
    };
}
