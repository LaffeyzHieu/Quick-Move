using Microsoft.VisualBasic.FileIO;

namespace QuickMove;

static class Mover
{
    // Copy hoặc move các file/thư mục vào destDir, dùng hộp thoại của Windows (tiến trình, trùng tên → Replace/Skip).
    // Trả về số mục đã xong.
    public static int Run(IEnumerable<string> sources, string destDir, bool copy, bool showDialogs = true)
    {
        destDir = History.Normalize(destDir);
        var ui = showDialogs ? UIOption.AllDialogs : UIOption.OnlyErrorDialogs;
        const UICancelOption onCancel = UICancelOption.ThrowException;
        int done = 0;
        var errors = new List<string>();

        foreach (var raw in sources)
        {
            var src = History.Normalize(raw);
            var parent = Path.GetDirectoryName(src);
            if (string.Equals(parent, destDir, StringComparison.OrdinalIgnoreCase)) continue; // đã ở đó rồi
            var target = Path.Combine(destDir, Path.GetFileName(src));
            try
            {
                bool dir = Directory.Exists(src);
                if (copy && dir) FileSystem.CopyDirectory(src, target, ui, onCancel);
                else if (copy) FileSystem.CopyFile(src, target, ui, onCancel);
                else if (dir) FileSystem.MoveDirectory(src, target, ui, onCancel);
                else FileSystem.MoveFile(src, target, ui, onCancel);
                if (copy || (!File.Exists(src) && !Directory.Exists(src))) done++;
            }
            catch (OperationCanceledException) { } // user bấm Cancel: không tính, không phải lỗi
            catch (Exception ex) { errors.Add($"{Path.GetFileName(src)}: {ex.Message}"); }
        }

        if (errors.Count > 0 && showDialogs)
            MessageBox.Show(string.Join("\n", errors), "Quick Move – lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        else if (errors.Count > 0)
            throw new IOException(string.Join("\n", errors));
        return done;
    }
}
