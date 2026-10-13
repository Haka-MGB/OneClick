using System.Windows;
using System.Windows.Controls;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>History and Undo page — FR-HIS-01 to FR-HIS-04.</summary>
public sealed class HistoryPage : UserControl
{
    private readonly MainWindow    _shell;
    private readonly JournalService _svc;
    private StackPanel _list = null!;

    public HistoryPage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<JournalService>();
        Build();
        Refresh();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("History",
            "Every change you have made. Select a batch to undo it."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        var exportBtn = new Button
        {
            Content             = "Export to CSV",
            Style               = Application.Current.FindResource("Button.Secondary") as Style,
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        exportBtn.Click += (_, _) => Export();
        content.Children.Add(exportBtn);

        var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
        _list = new StackPanel { Spacing = 4 };
        card.Child = _list;
        content.Children.Add(card);

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    public void Refresh()
    {
        _list.Children.Clear();
        var items = _svc.AllItems();

        if (items.Count == 0)
        {
            _list.Children.Add(PageLayout.EmptyState("No history yet. Actions you apply will appear here."));
            return;
        }

        foreach (var item in items)
        {
            var it  = item;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = it.Label, Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold });
            info.Children.Add(new TextBlock { Text = $"{it.ItemCount} files  •  {it.CreatedAt:d MMM yyyy, HH:mm}  •  {it.Status}", Style = Application.Current.FindResource("Text.Caption") as Style });
            Grid.SetColumn(info, 0);
            row.Children.Add(info);

            if (it.CanUndo)
            {
                var undoBtn = new Button
                {
                    Content   = "Undo",
                    Style     = Application.Current.FindResource("Button.Secondary") as Style,
                    Padding   = new Thickness(14, 6, 14, 6),
                    MinHeight = 0,
                    Margin    = new Thickness(12, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                undoBtn.Click += (_, _) =>
                {
                    var result = _svc.Undo(it.Id);
                    _shell.SetStatus(result is not null
                        ? $"Undone: {result.Label} ({result.ItemCount} files restored)."
                        : "Could not undo — a later batch conflicts. Undo the most recent batch first.");
                    _shell.SyncUndo(_svc.LastActionLabel());
                    Refresh();
                };
                Grid.SetColumn(undoBtn, 1);
                row.Children.Add(undoBtn);
            }

            _list.Children.Add(row);
        }
    }

    private void Export()
    {
        try
        {
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"OneClick-History-{DateTime.Now:yyyyMMdd}.csv");
            _svc.ExportCsv(path);
            _shell.SetStatus($"History exported to {path}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Export failed: {ex.Message}", "One Click");
        }
    }
}
