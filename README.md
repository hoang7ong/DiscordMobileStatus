# YT Status

**Tiếng Việt** · [English](README.en.md)

Hiện bạn đang nghe / xem gì trên **YouTube** và **YouTube Music** lên trạng thái Discord, giống ô Spotify:

```
ĐANG NGHE YOUTUBE MUSIC                 ĐANG XEM YOUTUBE
[ảnh bìa]  Tên bài                      [ảnh]  Tên video
           Ca sĩ                               Tên kênh
           ━━━━━━━○──── 1:23 / 4:08            ━━━━○────── 3:10 / 12:45
```

Nhận bài từ 2 nơi:

- **Trình duyệt trên máy tính** (Chrome, Edge, Cốc Cốc, Brave…): chỉ cần `YTStatus.exe`.
- **Điện thoại Android** (app YouTube, YouTube Music): cài thêm `YTStatus.apk`.

Discord chỉ cho đặt trạng thái kiểu này qua app Discord trên máy tính, nên trạng thái hiện khi PC đang bật và mở Discord (app desktop, không phải bản web).

Tải `YTStatus.exe` và `YTStatus.apk` ở mục **Releases** bên phải.

---

## 1. Tạo ứng dụng Discord (1 lần, 2 phút)

Trạng thái ghi **tên ứng dụng** ("Đang nghe **YouTube Music**"), nên mỗi người tự tạo một ứng dụng mang tên đó:

1. Vào https://discord.com/developers/applications → **New Application** → đặt tên `YouTube Music` → Create.
2. Copy dãy số **Application ID** ở trang General Information.
3. Tùy chọn: tạo thêm ứng dụng tên `YouTube` để lúc xem video ghi "Đang xem **YouTube**".
4. Tùy chọn — biểu tượng nhỏ góc ảnh bìa: trong ứng dụng › **Rich Presence › Art Assets** → thêm ảnh [`pc/anh_cho_discord/ytmusic.png`](pc/anh_cho_discord/ytmusic.png), đặt tên `ytmusic` (ứng dụng YouTube: `youtube.png`, tên `youtube`).

Không cần Bot, không cần token.

## 2. YTStatus.exe (máy tính)

1. Tải `YTStatus.exe`, để ở đâu cũng được (không cần cài), bấm đúp để chạy.
   Windows báo "Windows protected your PC" thì bấm **More info › Run anyway** (file không có chữ ký số).
2. Dán **Client ID** vào ô "YouTube Music" (và ô "YouTube" nếu có), bấm **Lưu**.
3. Tick **Chạy cùng Windows** nếu muốn bật máy là tự chạy.
4. Bấm **Thử Discord**: trạng thái hiện một bài mẫu trong 20 giây.

Mở YouTube trên trình duyệt phát một bài là trạng thái Discord đổi theo.

| Thao tác | Kết quả |
|---|---|
| Bấm **X** | Ẩn xuống khay hệ thống, app vẫn chạy |
| Bấm biểu tượng ở khay | Mở lại cửa sổ |
| Chuột phải biểu tượng › **Thoát** | Tắt hẳn |
| Tick **Tắt hiện lên Discord** | App vẫn chạy nhưng không hiện gì |
| Bỏ tick **Chia sẻ YouTube Music** / **Chia sẻ YouTube** | Tạm không hiện loại đó (cũng có trong menu chuột phải ở khay) |
| Ô **Tiếng Việt / English** (góc trên) | Đổi ngôn ngữ ngay |

Cấu hình lưu ở `YTStatus.json` cạnh file exe. Muốn bỏ: bỏ tick "Chạy cùng Windows", Thoát, xóa file.

## 3. YTStatus.apk (điện thoại Android — tùy chọn)

1. Cài `YTStatus.apk`. Ở Việt Nam, **Google Play Protect sẽ chặn** vì app cần quyền đọc thông báo (để biết bài đang phát). Cách cài:
   - Mở **Play Store** › ảnh đại diện › **Play Protect** › ⚙ › tắt **Quét ứng dụng bằng Play Protect** → cài APK → **bật lại** ngay.
2. Mở app → bật quyền **Truy cập thông báo** cho YT Status.
   Công tắc bị mờ (Android 13+): Cài đặt › Ứng dụng › YT Status › dấu **⋮** › **Cho phép cài đặt bị hạn chế**, rồi bật lại.
3. Bấm **Không tối ưu pin** (Xiaomi / Oppo / Samsung bật thêm "Tự khởi chạy").
4. Nút **English / Tiếng Việt** góc trên đổi ngôn ngữ app.
5. Chép **Mã** trong app (dạng `nc-xxxx-xxxx-xxxx`) dán vào ô **Mã điện thoại** trong YTStatus.exe, bấm Lưu.

Điện thoại gửi bài qua [ntfy.sh](https://ntfy.sh) (miễn phí, 250 tin/ngày — app chỉ gửi khi đổi bài / tạm dừng / tua nên đủ dùng cả ngày). Đừng đăng mã của bạn công khai.

Đang phát cả trên PC lẫn điện thoại thì trạng thái hiện bài trên PC.

---

## Gặp lỗi

| Hiện tượng | Cách xử lý |
|---|---|
| Discord không hiện gì | Xem các dòng trạng thái trong cửa sổ YTStatus. Kiểm tra: đã bỏ tick "Tắt hiện lên Discord"; đang mở Discord **app desktop**; Discord › Cài đặt › Quyền riêng tư hoạt động › bật "Hiển thị hoạt động hiện tại" |
| Dòng "Máy tính" báo "không phải tab YouTube" | Đang phát nhạc ở app khác (Spotify…) — app chỉ nhận YouTube |
| Điện thoại khóa màn hình một lúc thì ngừng | Tắt tối ưu pin + bật tự khởi chạy cho app |
| Lỗi khác | Gửi file `YTStatus.log` (cạnh exe) cho người chia sẻ app |

---

## Tự build từ mã nguồn

- `BUILD_EXE.bat` — build YTStatus.exe bằng trình biên dịch có sẵn trong Windows (vài giây).
- `BUILD_APK.bat` — build YTStatus.apk (lần đầu tải JDK + công cụ Android ~600 MB). Tự tạo khóa ký mới ở `android/khoa_ky/` nếu chưa có.
- `CAI_QUA_USB.bat` — cài APK qua cáp USB (không bị Play Protect chặn).
