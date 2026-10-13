using System.Windows;
using System.Windows.Controls;

namespace OneClick.App.Pages;

/// <summary>
/// Laptop Migration Checklist — FR-MIG-01.
/// R2 delivers the checklist only; the copy engine arrives in R3.
/// </summary>
public sealed class MigrationPage : UserControl
{
    private readonly MainWindow _shell;

    private static readonly (string Category, string Description, string Tip)[] Items =
    {
        ("Documents",          "Your files in Documents, Desktop, Downloads, and any other folders you use.",
         "Plug in an external drive and drag the folders over, or use a cloud sync service."),
        ("Pictures and Videos","Photos and videos in Pictures, Videos, or your phone backup folder.",
         "Check you also copy photos synced from your phone."),
        ("Browser bookmarks",  "Export bookmarks from Chrome, Edge, or Firefox before you move.",
         "In Chrome: Settings → Bookmarks → Export. In Firefox: Library → Import and Backup → Export."),
        ("Email",              "If you use a desktop email app, check it is synced to a server so messages are on the new machine when you sign in.",
         "Web-based email (Gmail, Outlook.com) is already safe — just sign in again."),
        ("Software licences",  "Write down licence keys for paid programs you will reinstall.",
         "Check emails with subject lines like 'Your order' or 'Product key'."),
        ("Wi-Fi password",     "Note your Wi-Fi name and password so you can connect the new computer.",
         "Find it on your router or in Windows Settings → Network → Wi-Fi → Manage known networks."),
        ("Printer setup",      "Note the printer model so you can download the driver on the new machine.",
         "Most modern printers are found automatically on a local network."),
        ("Cloud accounts",     "Write down which cloud services you use (OneDrive, Google Drive, iCloud).",
         "You just need to sign in again on the new computer."),
    };

    public MigrationPage(MainWindow shell)
    {
        _shell = shell;
        Build();
    }

    private void Build()
    {
        var root  = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var outer = new StackPanel();
        outer.Children.Add(PageLayout.PageHeader("Migration Checklist",
            "Everything to move when switching to a new computer. Tick each item as you go."));

        var content = new StackPanel { Margin = new Thickness(32, 0, 32, 32), Spacing = 14 };

        content.Children.Add(PageLayout.Banner(
            "Go through each item before you hand over or sell your old computer. " +
            "Nothing is copied automatically — this is a guide to make sure you do not miss anything.",
            BannerKind.Info));

        foreach (var (category, description, tip) in Items)
        {
            var card  = new Border { Style = Application.Current.FindResource("Card") as Style };
            var stack = new StackPanel { Spacing = 6 };

            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
            var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
            var title = new TextBlock
            {
                Text       = category,
                Style      = Application.Current.FindResource("Text.Heading") as Style,
                VerticalAlignment = VerticalAlignment.Center,
            };
            // Strike through when checked
            cb.Checked   += (_, _) => title.TextDecorations = TextDecorations.Strikethrough;
            cb.Unchecked += (_, _) => title.TextDecorations = null;

            headerRow.Children.Add(cb);
            headerRow.Children.Add(title);
            stack.Children.Add(headerRow);

            stack.Children.Add(new TextBlock
            {
                Text       = description,
                Style      = Application.Current.FindResource("Text.Body") as Style,
                Margin     = new Thickness(28, 0, 0, 0),
            });
            stack.Children.Add(new TextBlock
            {
                Text       = $"Tip: {tip}",
                Style      = Application.Current.FindResource("Text.Secondary") as Style,
                FontStyle  = FontStyles.Italic,
                Margin     = new Thickness(28, 0, 0, 0),
                TextWrapping = TextWrapping.Wrap,
            });

            card.Child = stack;
            content.Children.Add(card);
        }

        // After the move
        var afterCard = new Border { Style = Application.Current.FindResource("Card") as Style };
        var afterStack = new StackPanel { Spacing = 8 };
        afterStack.Children.Add(new TextBlock { Text = "After the move", Style = Application.Current.FindResource("Text.Heading") as Style });
        foreach (var step in new[]
        {
            "Sign in to your cloud services (OneDrive, Google Drive).",
            "Open your browser and import the bookmarks you exported.",
            "Check that all your files arrived by opening a few.",
            "Reinstall programs you need and enter any licence keys.",
            "Connect your printer and check it works.",
            "Only wipe or sell the old computer after you are happy everything is safe.",
        })
        {
            afterStack.Children.Add(new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    new CheckBox { VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 2, 0, 0) },
                    new TextBlock { Text = step, Style = Application.Current.FindResource("Text.Body") as Style, TextWrapping = TextWrapping.Wrap },
                }
            });
        }
        afterCard.Child = afterStack;
        content.Children.Add(afterCard);

        outer.Children.Add(content);
        root.Content = outer;
        Content = root;
    }
}
