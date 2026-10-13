using System.Windows;
using System.Windows.Controls;
using OneClick.App.Services;

namespace OneClick.App.Pages;

/// <summary>
/// Automation Rules page — FR-AUT-01 to FR-AUT-12.
/// Shows active rules, template library, and rule builder.
/// </summary>
public sealed class AutomationPage : UserControl
{
    private readonly MainWindow       _shell;
    private readonly AutomationService _svc;
    private StackPanel _rulesPanel    = null!;
    private StackPanel _templatesPanel = null!;

    public AutomationPage(MainWindow shell)
    {
        _shell = shell;
        _svc   = ServiceLocator.Get<AutomationService>();
        Build();
        RefreshRules();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Automation",
            "Set up rules that organise files automatically. Rules only run after you review a dry run."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        // My Rules
        var rulesCard = new Border { Style = Application.Current.FindResource("Card") as Style };
        var rulesStack = new StackPanel { Spacing = 10 };
        var rulesHeader = new Grid();
        rulesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        rulesHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        rulesHeader.Children.Add(new TextBlock { Text = "My rules", Style = Application.Current.FindResource("Text.Heading") as Style });

        _rulesPanel = new StackPanel { Spacing = 6 };
        rulesStack.Children.Add(rulesHeader);
        rulesStack.Children.Add(_rulesPanel);
        rulesCard.Child = rulesStack;
        content.Children.Add(rulesCard);

        // Template library (FR-AUT-08)
        content.Children.Add(BuildTemplateLibrary());

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }

    private Border BuildTemplateLibrary()
    {
        var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
        var stack = new StackPanel { Spacing = 10 };

        stack.Children.Add(new TextBlock
        {
            Text  = "Template library",
            Style = Application.Current.FindResource("Text.Heading") as Style,
        });
        stack.Children.Add(new TextBlock
        {
            Text  = "Choose a ready-made rule. It opens with a dry run so you can see what it would do before it is enabled.",
            Style = Application.Current.FindResource("Text.Secondary") as Style,
        });

        _templatesPanel = new StackPanel { Spacing = 8 };

        foreach (var t in _svc.GetTemplates())
        {
            var tmpl = t;
            var row  = new Border
            {
                Style  = Application.Current.FindResource("Banner.Info") as Style,
                Margin = new Thickness(0),
            };
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = tmpl.Name,        Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold });
            info.Children.Add(new TextBlock { Text = tmpl.Description, Style = Application.Current.FindResource("Text.Caption") as Style });
            info.Children.Add(new TextBlock { Text = $"Example: {tmpl.Example}", Style = Application.Current.FindResource("Text.Caption") as Style, FontStyle = FontStyles.Italic });
            Grid.SetColumn(info, 0);
            grid.Children.Add(info);

            var useBtn = new Button
            {
                Content = "Use template",
                Style   = Application.Current.FindResource("Button.Primary") as Style,
                Padding = new Thickness(12, 6, 12, 6),
                MinHeight = 0,
                VerticalAlignment = VerticalAlignment.Center,
                Margin  = new Thickness(12, 0, 0, 0),
            };
            useBtn.Click += (_, _) => ShowDryRun(tmpl);
            Grid.SetColumn(useBtn, 1);
            grid.Children.Add(useBtn);

            row.Child = grid;
            _templatesPanel.Children.Add(row);
        }

        stack.Children.Add(_templatesPanel);
        card.Child = stack;
        return card;
    }

    private void RefreshRules()
    {
        _rulesPanel.Children.Clear();
        var rules = _svc.GetRules();

        if (rules.Count == 0)
        {
            _rulesPanel.Children.Add(PageLayout.EmptyState(
                "No rules yet. Start from a template below."));
            return;
        }

        foreach (var rule in rules)
        {
            var r   = rule;
            var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = r.Name,    Style = Application.Current.FindResource("Text.Body") as Style, FontWeight = FontWeights.SemiBold });
            info.Children.Add(new TextBlock { Text = r.Summary, Style = Application.Current.FindResource("Text.Caption") as Style });
            Grid.SetColumn(info, 0);
            row.Children.Add(info);

            var toggle = new CheckBox
            {
                IsChecked         = r.IsEnabled,
                VerticalAlignment = VerticalAlignment.Center,
                Margin            = new Thickness(12, 0, 0, 0),
                Content           = r.IsEnabled ? "Active" : "Paused",
            };
            toggle.Checked   += (_, _) => { _svc.SetEnabled(r.Id, true);  _shell.SetStatus($"Rule '{r.Name}' enabled."); };
            toggle.Unchecked += (_, _) => { _svc.SetEnabled(r.Id, false); _shell.SetStatus($"Rule '{r.Name}' paused."); };
            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);

            _rulesPanel.Children.Add(row);
        }
    }

    private void ShowDryRun(RuleTemplate tmpl)
    {
        // Dry run is mandatory before enabling (FR-AUT-09)
        var results = _svc.DryRun(tmpl);
        string msg;
        if (results.Count == 0)
        {
            msg = $"Dry run for \"{tmpl.Name}\": no files would be affected right now.";
        }
        else
        {
            msg = $"Dry run for \"{tmpl.Name}\":\n\n"
                + string.Join("\n", results.Take(10).Select(r => $"  {r}"))
                + (results.Count > 10 ? $"\n  ...and {results.Count - 10} more." : string.Empty);
        }

        if (MessageBox.Show(msg + "\n\nEnable this rule?", "One Click - Automation",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _svc.CreateFromTemplate(tmpl);
            RefreshRules();
            _shell.SetStatus($"Rule '{tmpl.Name}' created and enabled.");
        }
    }
}
