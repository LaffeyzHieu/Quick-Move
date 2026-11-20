namespace QuickMove;

// Kiểm tra logic không cần chuột: chạy "QuickMove.exe --selftest" (nên đặt QUICKMOVE_DATA ra thư mục nháp).
static class SelfTest
{
    public static int Run()
    {
        int fail = 0;
        void Check(bool ok, string name) { Console.WriteLine((ok ? "PASS " : "FAIL ") + name); if (!ok) fail++; }

        var root = Path.Combine(Path.GetTempPath(), "QuickMoveTest_" + Guid.NewGuid().ToString("N")[..8]);
        var src = Directory.CreateDirectory(Path.Combine(root, "src")).FullName;
        var mods = Directory.CreateDirectory(Path.Combine(root, "mods")).FullName;
        var dl = Directory.CreateDirectory(Path.Combine(root, "Downloads")).FullName;
        var adb = Directory.CreateDirectory(Path.Combine(root, "adb")).FullName;
        try
        {
            // 1. Lịch sử: Most used / Last use / History n
            var h = new History(Path.Combine(root, "h.json"));
            var t = new DateTime(2026, 11, 20, 10, 0, 0);
            h.Record(mods, t); h.Record(mods, t.AddMinutes(1)); h.Record(mods, t.AddMinutes(2));
            h.Record(adb, t.AddMinutes(3));
            h.Record(dl, t.AddMinutes(4));
            var s = h.Slots();
            Check(s[0] == new Slot("Most used", mods), "slot 0 = Most used mods");
            Check(s[1] == new Slot("Last use", dl), "slot 1 = Last use Downloads");
            Check(s[2] == new Slot("History 1", adb), "slot 2 = History 1 adb");
            Check(s[^1].Path == null, "slot cuối = chọn thư mục khác");

            // 2. Lưu & đọc lại
            var h2 = History.Load(Path.Combine(root, "h.json"));
            Check(h2.Entries.Count == 3 && h2.Entries.First(e => e.Path == mods).Count == 3, "lưu/đọc history.json");

            // 3. Thư mục bị xoá thì không gợi ý nữa
            Check(!h.Slots(exists: p => p != adb).Any(x => x.Path == adb), "bỏ thư mục không tồn tại");

            // 4. Move thật: 1 file + 1 thư mục
            var f = Path.Combine(src, "a.jar"); File.WriteAllText(f, "hi");
            var d = Directory.CreateDirectory(Path.Combine(src, "folder")).FullName; File.WriteAllText(Path.Combine(d, "x.txt"), "x");
            int n = Mover.MoveInto(new[] { f, d }, mods, showDialogs: false);
            Check(n == 2 && File.Exists(Path.Combine(mods, "a.jar")) && File.Exists(Path.Combine(mods, "folder", "x.txt")) && !File.Exists(f), "move file + thư mục");

            // 5. Move vào chính thư mục đang chứa → bỏ qua
            Check(Mover.MoveInto(new[] { Path.Combine(mods, "a.jar") }, mods, false) == 0, "bỏ qua khi đã ở đích");
        }
        finally { try { Directory.Delete(root, true); } catch { } }

        Console.WriteLine(fail == 0 ? "ALL PASS" : $"{fail} FAIL");
        return fail;
    }

    // Vẽ popup ra file PNG để xem giao diện.
    public static void Snapshot(string png)
    {
        var dir = Path.Combine(Path.GetTempPath(), "QuickMoveSnap");
        var names = new[] { "mods", "Downloads", "AppData", "adb" };
        foreach (var n in names) Directory.CreateDirectory(Path.Combine(dir, n));
        var h = new History(Path.Combine(dir, "h.json"));
        var t = DateTime.Now;
        h.Record(Path.Combine(dir, "mods"), t.AddMinutes(-9)); h.Record(Path.Combine(dir, "mods"), t.AddMinutes(-8));
        h.Record(Path.Combine(dir, "adb"), t.AddMinutes(-3));
        h.Record(Path.Combine(dir, "AppData"), t.AddMinutes(-2));
        h.Record(Path.Combine(dir, "Downloads"), t.AddMinutes(-1));

        using var p = new MovePopup(h);
        p.ShowNear(new Point(200, 200));
        Application.DoEvents();
        using var bmp = new Bitmap(p.Width, p.Height);
        p.DrawToBitmap(bmp, new Rectangle(Point.Empty, p.Size));
        bmp.Save(png);
        p.Hide();
    }
}
