using Microsoft.VisualBasic.FileIO;

namespace QuickMove;

static class Mover
{
    // Move các file/thư mục vào destDir. Dùng hộp thoại của Windows (tiến trình, trùng tên → Replace/Skip).
    // Trả về số mục đã move được.
    public static int MoveInto(IEnumerable<string> sources, string destDir, bool showDialogs = true)
    {
        destDir = History.Normalize(destDir);
        var ui = showDialogs ? UIOption.AllDialogs : UIOption.OnlyErrorDialogs;
        int moved = 0;
        var errors = new List<string>();

        foreach (var raw in sources)
        {
            var src = History.Normalize(raw);
            var parent = Path.GetDirectoryName(src);
            if (string.Equals(parent, destDir, StringComparison.OrdinalIgnoreCase)) continue; // đã ở đó rồi
            var target = Path.Combine(destDir, Path.GetFileName(src));
            try
            {
                if (Directory.Exists(src)) FileSystem.MoveDirectory(src, target, ui, UICancelOption.DoNothing);
                else FileSystem.MoveFile(src, target, ui, UICancelOption.DoNothing);
                if (!File.Exists(src) && !Directory.Exists(src)) moved++;
            }
            catch (Exception ex) { errors.Add($"{Path.GetFileName(src)}: {ex.Message}"); }
        }

        if (errors.Count > 0 && showDialogs)
            MessageBox.Show(string.Join("\n", errors), "Quick Move – lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else if (errors.Count > 0)
            throw new IOException(string.Join("\n", errors));
        return moved;
    }
}
