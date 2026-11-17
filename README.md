# Discord Mobile Status

**Tiếng Việt** · [English](README.en.md)

*(Tên cũ: YT Status)*

Hiện bạn đang **nghe / xem / chơi** gì lên trạng thái Discord, giống ô Spotify:

```
ĐANG NGHE YOUTUBE MUSIC          ĐANG CHƠI
[ảnh bìa]  Tên bài               [icon]  Liên Quân Mobile
           Ca sĩ                         Trên điện thoại
           ━━━━━○──── 1:23 / 4:08         ⏱ 23:14 đã trôi qua
```

Nhận từ:

- **YouTube / YouTube Music trên trình duyệt máy tính** (Chrome, Edge, Cốc Cốc, Brave…): chỉ cần `DiscordMobileStatus.exe`.
- **YouTube / YouTube Music trên điện thoại Android**: cài thêm `DiscordMobileStatus.apk`.
- **Game trên điện thoại Android** (Liên Quân Mobile, Liên Minh Tốc Chiến, PUBG Mobile, Free Fire…): hiện tên game, icon và thời gian đã chơi. App **không** đọc tướng, rank, tỉ số hay bất cứ dữ liệu nào trong game.

Nhạc và game hiện **cùng lúc** được (vừa nghe nhạc vừa chơi game).

Discord chỉ cho đặt trạng thái kiểu này qua app Discord trên máy tính, nên trạng thái hiện khi PC đang bật và mở Discord (app desktop, không phải bản web).

Tải `DiscordMobileStatus.exe` và `DiscordMobileStatus.apk` ở mục **Releases** bên phải.

Cả hai app có nút **Tiếng Việt / English**. Lần đầu mở sẽ theo ngôn ngữ của Windows / điện thoại.

---

## 1. DiscordMobileStatus.exe (máy tính)

Không cần tạo ứng dụng Discord hay bật Developer Mode — app đã có sẵn các ứng dụng "YouTube Music", "YouTube" và "Game trên điện thoại".

1. Tải `DiscordMobileStatus.exe`, để ở đâu cũng được (không cần cài), bấm đúp để chạy.
   Windows báo "Windows protected your PC" thì bấm **More info › Run anyway** (file không có chữ ký số).
2. Mở Discord trên máy tính, phát một bài YouTube trên trình duyệt — xong.
3. Bấm **Thử Discord** để xem trước: trạng thái hiện một bài mẫu trong 20 giây.

Cửa sổ chia thành các khối, mỗi mục là một công tắc:

| Khối | Công tắc |
|---|---|
| **Chia sẻ** | **Chia sẻ lên Discord** — tắt là ẩn hết, các công tắc con thu lại. Bật mới hiện: YouTube Music, YouTube, Game trên điện thoại, Vẫn hiện khi tạm dừng |
| **Nguồn** | **Trình duyệt trên máy này**; **Điện thoại Android** — bật mới hiện ô nhập mã điện thoại |
| **Ứng dụng** | **Chạy cùng Windows** (tự chạy ẩn dưới khay) |

| Thao tác | Kết quả |
|---|---|
| Bấm **X** | Ẩn xuống khay hệ thống, app vẫn chạy |
| Bấm biểu tượng ở khay | Mở lại cửa sổ |
| Chuột phải biểu tượng | Bật/tắt nhanh từng loại, **Thoát** để tắt hẳn |
| Nút **Tiếng Việt / English** (góc trên) | Đổi ngôn ngữ ngay |

Cấu hình lưu ở `DiscordMobileStatus.json` cạnh file exe. Đang dùng bản cũ thì cấu hình được tự chuyển sang.

## 2. DiscordMobileStatus.apk (điện thoại Android — tùy chọn)

1. Cài `DiscordMobileStatus.apk` (cài đè lên YT Status cũ được, mã giữ nguyên). Ở Việt Nam, **Google Play Protect sẽ chặn** vì app cần quyền đọc thông báo. Cách cài:
   - Mở **Play Store** › ảnh đại diện › **Play Protect** › ⚙ › tắt **Quét ứng dụng bằng Play Protect** → cài APK → **bật lại** ngay.
2. Mở app → bật quyền **Truy cập thông báo** (để biết bài đang phát).
   Công tắc bị mờ (Android 13+): Cài đặt › Ứng dụng › Discord Mobile Status › dấu **⋮** › **Cho phép cài đặt bị hạn chế**, rồi bật lại.
3. Muốn chia sẻ game: bấm **Mở cài đặt truy cập dữ liệu sử dụng** → bật cho Discord Mobile Status. Quyền này giống Digital Wellbeing — chỉ biết app nào đang mở.
4. Bấm **Không tối ưu pin** (Xiaomi / Oppo / Samsung bật thêm "Tự khởi chạy").
5. Khối **Chia sẻ** › bật **Game** › **Chọn game được chia sẻ…**: game tự nhận diện được bật sẵn; tick thêm app khác nếu muốn, bỏ tick game không muốn ai thấy. App không phải game thì không bao giờ bị chia sẻ nếu bạn không tick.
6. Trên máy tính, khối **Nguồn** › bật **Điện thoại Android**, dán **Mã** trong app (dạng `nc-xxxx-xxxx-xxxx`), bấm Lưu.

Điện thoại gửi qua [ntfy.sh](https://ntfy.sh) (miễn phí, 250 tin/ngày). App chỉ gửi khi đổi bài / tạm dừng / tua / vào hoặc thoát game, nên đủ dùng cả ngày. Đừng đăng mã của bạn công khai.

Pin: game chỉ được kiểm tra 15 giây một lần **khi màn hình bật**; tắt màn hình thì ngừng hẳn. Thoát game ra chat hay xem video chưa tới 2 phút thì vẫn tính là đang chơi.

Nghe nhạc trên cả PC lẫn điện thoại cùng lúc thì hiện bài trên PC.

---

## Gặp lỗi

| Hiện tượng | Cách xử lý |
|---|---|
| Discord không hiện gì | Xem các dòng trạng thái trong cửa sổ. Kiểm tra: đã bật "Chia sẻ lên Discord"; đang mở Discord **app desktop**; Discord › Cài đặt › Quyền riêng tư hoạt động › bật "Hiển thị hoạt động hiện tại" |
| Có nhạc nhưng game không hiện (hoặc ngược lại) | Báo lại cho người chia sẻ app kèm file `DiscordMobileStatus.log` |
| Dòng "Máy tính" báo "không phải tab YouTube" | Đang phát nhạc ở app khác (Spotify…) — app chỉ nhận YouTube |
| Điện thoại khóa màn hình một lúc thì ngừng | Tắt tối ưu pin + bật tự khởi chạy cho app |
| Lỗi khác | Gửi file `DiscordMobileStatus.log` (cạnh exe) cho người chia sẻ app |

---

## Tự build từ mã nguồn

- `BUILD_EXE.bat` — build DiscordMobileStatus.exe bằng trình biên dịch có sẵn trong Windows (vài giây).
- `BUILD_APK.bat` — build DiscordMobileStatus.apk (lần đầu tải JDK + công cụ Android ~600 MB).
- `CAI_QUA_USB.bat` — cài APK qua cáp USB (không bị Play Protect chặn).
