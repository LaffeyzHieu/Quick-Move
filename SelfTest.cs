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
            int n = Mover.Run(new[] { f, d }, mods, copy: false, showDialogs: false);
            Check(n == 2 && File.Exists(Path.Combine(mods, "a.jar")) && File.Exists(Path.Combine(mods, "folder", "x.txt")) && !File.Exists(f), "move file + thư mục");

            // 5. Move vào chính thư mục đang chứa → bỏ qua
            Check(Mover.Run(new[] { Path.Combine(mods, "a.jar") }, mods, copy: false, showDialogs: false) == 0, "bỏ qua khi đã ở đích");

            // 6. Copy thật: bản gốc phải còn nguyên
            f = Path.Combine(mods, "a.jar"); d = Path.Combine(mods, "folder");
            n = Mover.Run(new[] { f, d }, adb, copy: true, showDialogs: false);
            Check(n == 2 && File.Exists(f) && File.Exists(Path.Combine(d, "x.txt"))
                && File.ReadAllText(Path.Combine(adb, "a.jar")) == "hi" && File.Exists(Path.Combine(adb, "folder", "x.txt")), "copy file + thư mục, giữ bản gốc");

            // 7. Thư mục vào chính thư mục con của nó → báo lỗi, không mất gì
            foreach (var copy in new[] { true, false })
            {
                bool threw = false;
                try { Mover.Run(new[] { mods }, d, copy, showDialogs: false); } catch (IOException) { threw = true; }
                Check(threw && File.Exists(f) && File.Exists(Path.Combine(d, "x.txt")), $"thư mục vào chính nó, copy={copy}");
            }

            // 8. Thả vào đúng popup thật: nửa trái = copy, nửa phải = move
            using var popup = new MovePopup(h);
            _ = popup.Handle;
            popup.Rebuild();
            DragDropEffects Drop(string file, float at, DragDropEffects allowed = DragDropEffects.All)
            {
                var row = popup.Controls.OfType<SlotRow>().Single(r => r.AccessibleName == "Last use " + dl);
                var e = Drag(row, at, allowed, file);
                IDropTarget target = row;
                target.OnDragEnter(e);
                var shown = e.Effect;
                target.OnDragDrop(e);
                Check(e.Effect == DragDropEffects.None, "trả None cho Explorer");
                Application.DoEvents();
                return shown;
            }
            var c = Path.Combine(src, "c.txt"); File.WriteAllText(c, "c");
            var m = Path.Combine(src, "m.txt"); File.WriteAllText(m, "m");
            var ro = Path.Combine(src, "ro.txt"); File.WriteAllText(ro, "r");
            Check(Drop(c, 0.1f) == DragDropEffects.Copy && File.Exists(c) && File.Exists(Path.Combine(dl, "c.txt")), "thả nửa trái → copy");
            Check(Drop(m, 0.9f) == DragDropEffects.Move && !File.Exists(m) && File.Exists(Path.Combine(dl, "m.txt")), "thả nửa phải → move");
            Check(Drop(ro, 0.9f, DragDropEffects.Copy) == DragDropEffects.None && File.Exists(ro), "nguồn cấm move → không làm gì");
            Check(h.Entries.Single(x => x.Path == dl).Count == 3, "copy và move đều vào lịch sử");
        }
        finally { try { Directory.Delete(root, true); } catch { } }

        Console.WriteLine(fail == 0 ? "ALL PASS" : $"{fail} FAIL");
        return fail;
    }

    static DragEventArgs Drag(Control row, float at, DragDropEffects allowed, string file)
    {
        var p = row.PointToScreen(new Point((int)(row.Width * at), row.Height / 2));
        return new DragEventArgs(new DataObject(DataFormats.FileDrop, new[] { file }), 0, p.X, p.Y, allowed, DragDropEffects.None);
    }

    // Vẽ popup ra file PNG để xem giao diện (ô 1 đang trỏ Copy, ô 2 đang trỏ Move).
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
        var rows = p.Controls.OfType<SlotRow>().ToList();
        for (int i = 0; i < 2; i++)
            ((IDropTarget)rows[i]).OnDragEnter(Drag(rows[i], i == 0 ? 0.1f : 0.9f, DragDropEffects.All, png));
        Application.DoEvents();
        using var bmp = new Bitmap(p.Width, p.Height);
        p.DrawToBitmap(bmp, new Rectangle(Point.Empty, p.Size));
        bmp.Save(png);
        p.Hide();
    }
}
