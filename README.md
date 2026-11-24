# Quick Move

App nhỏ chạy dưới khay hệ thống (Windows). Kéo file trong File Explorer (hoặc trên Desktop), **giữ yên 2 giây** thì hiện popup với các thư mục hay dùng. Kéo vào ô nào thì ô đó tách đôi: thả bên trái là **Copy to**, bên phải là **Move to**.

- Tối đa 5 ô: Most used, Last use, History 1–2, và "Chọn thư mục khác…"
- Kéo chuột ra xa popup quá 120px thì popup tự đóng; giữ yên 2s nữa thì mở lại
- Chuột phải icon dưới khay: bật/tắt "Chạy cùng Windows", mở thư mục dữ liệu, thoát

## Cài đặt

Cần [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0). Tự build:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o publish
```

Ra một file `publish\QuickMove.exe`.

## Cách hoạt động

Explorer không phát ra event kéo/hover cho app khác, nên app hỏi trạng thái chuột mỗi 30ms (`DragWatcher.cs`). Để biết đang kéo file thật (chứ không phải kéo khung chọn), app tìm cửa sổ `SysDragImage`, là ảnh mờ của file mà Explorer vẽ lúc kéo. Các ô trong popup là drop target. App tự copy/move file, rồi báo cho Explorer là "không làm gì" để Explorer không tự xoá file gốc.

Lịch sử lưu ở `%LocalAppData%\QuickMove\history.json` (đổi chỗ bằng biến môi trường `QUICKMOVE_DATA`).

## Tham số

- `--selftest`: chạy kiểm tra logic + copy/move thật trong thư mục tạm
- `--snapshot <file.png>`: vẽ popup ra ảnh
- `--autostart on|off`: bật/tắt chạy cùng Windows
