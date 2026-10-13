namespace OneClick.App.Services;

public record JournalItem(Guid Id, string Label, int ItemCount, DateTime CreatedAt, string Status, bool CanUndo);
public record UndoResult(string Label, int ItemCount);

public class JournalService
{
    private readonly List<JournalItem> _items = [];

    public List<JournalItem> RecentItems(int count) =>
        _items.OrderByDescending(i => i.CreatedAt).Take(count).ToList();

    public List<JournalItem> AllItems() =>
        _items.OrderByDescending(i => i.CreatedAt).ToList();

    public string? LastActionLabel() =>
        _items.OrderByDescending(i => i.CreatedAt).FirstOrDefault()?.Label;

    public void Record(string label, int itemCount)
    {
        _items.Add(new JournalItem(Guid.NewGuid(), label, itemCount, DateTime.Now, "Complete", true));
    }

    public UndoResult? UndoLast()
    {
        var last = _items.OrderByDescending(i => i.CreatedAt).FirstOrDefault(i => i.CanUndo);
        if (last is null) return null;
        _items.Remove(last);
        // In the real implementation this calls the infrastructure undo service
        return new UndoResult(last.Label, last.ItemCount);
    }

    public UndoResult? Undo(Guid id)
    {
        var item = _items.FirstOrDefault(i => i.Id == id);
        if (item is null || !item.CanUndo) return null;
        // Check for conflicting later batches
        var laterConflict = _items.Any(i => i.CreatedAt > item.CreatedAt && i.CanUndo);
        if (laterConflict) return null;
        _items.Remove(item);
        return new UndoResult(item.Label, item.ItemCount);
    }

    public void ExportCsv(string path)
    {
        var lines = new List<string> { "Date,Tool,Files,Status" };
        lines.AddRange(_items.Select(i => $"{i.CreatedAt:O},{i.Label},{i.ItemCount},{i.Status}"));
        File.WriteAllLines(path, lines);
    }
}
