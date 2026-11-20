using System.Text.Json;

namespace QuickMove;

public class HistoryEntry
{
    public string Path { get; set; } = "";
    public int Count { get; set; }
    public DateTime LastUsed { get; set; }
}

public record Slot(string Label, string? Path);

// Lưu các thư mục đích đã từng move tới, để gợi ý trong popup.
public class History
{
    const int MaxEntries = 50;
    readonly string _file;
    public List<HistoryEntry> Entries { get; private set; } = new();

    public History(string file) { _file = file; }

    public static string DataDir()
    {
        var dir = Environment.GetEnvironmentVariable("QUICKMOVE_DATA");
        if (string.IsNullOrWhiteSpace(dir))
            dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickMove");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static History Load(string file)
    {
        var h = new History(file);
        try
        {
            if (File.Exists(file))
                h.Entries = JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(file)) ?? new();
        }
        catch { /* file hỏng thì bắt đầu lại từ đầu */ }
        return h;
    }

    public static string Normalize(string path) =>
        System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(path));

    public void Record(string folder, DateTime? when = null)
    {
        folder = Normalize(folder);
        var e = Entries.FirstOrDefault(x => string.Equals(x.Path, folder, StringComparison.OrdinalIgnoreCase));
        if (e == null) Entries.Add(e = new HistoryEntry { Path = folder });
        e.Count++;
        e.LastUsed = when ?? DateTime.Now;
        if (Entries.Count > MaxEntries)
            Entries = Entries.OrderByDescending(x => x.LastUsed).Take(MaxEntries).ToList();
        Save();
    }

    public void Save()
    {
        try { File.WriteAllText(_file, JsonSerializer.Serialize(Entries, new JsonSerializerOptions { WriteIndented = true })); }
        catch { }
    }

    // Most used, Last use, History 1, History 2... (không trùng nhau), cuối cùng là "chọn thư mục khác".
    public List<Slot> Slots(int max = 4, Func<string, bool>? exists = null)
    {
        exists ??= Directory.Exists;
        var alive = Entries.Where(e => exists(e.Path)).ToList();
        var slots = new List<Slot>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var most = alive.OrderByDescending(e => e.Count).ThenByDescending(e => e.LastUsed).FirstOrDefault();
        if (most != null && most.Count > 1) { slots.Add(new Slot("Most used", most.Path)); used.Add(most.Path); }

        int n = 0;
        foreach (var e in alive.OrderByDescending(e => e.LastUsed))
        {
            if (slots.Count >= max) break;
            if (!used.Add(e.Path)) { n = n == 0 ? 1 : n; continue; }
            slots.Add(new Slot(n == 0 ? "Last use" : $"History {n}", e.Path));
            n++;
        }
        slots.Add(new Slot("Chọn thư mục khác…", null));
        return slots;
    }

    public static string ShortName(string path)
    {
        var name = System.IO.Path.GetFileName(path);
        return string.IsNullOrEmpty(name) ? path : "\\" + name;
    }
}
