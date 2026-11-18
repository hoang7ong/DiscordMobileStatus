# Discord Mobile Status

**Tiếng Việt** · [English](README.en.md)

Hiện bạn đang **nghe, xem, lướt hay chơi gì** lên trạng thái Discord — giống ô Spotify, nhưng cho YouTube, SoundCloud, TikTok, Instagram và game điện thoại.

```
ĐANG NGHE YOUTUBE MUSIC          ĐANG CHƠI
[ảnh bìa]  Tên bài               [icon]  Liên Quân Mobile
           Ca sĩ                         Trên điện thoại
           ━━━━━○──── 1:23 / 4:08         ⏱ 23:14
```

## Tính năng

| | Máy tính | Điện thoại | Mặc định |
|---|:-:|:-:|:-:|
| YouTube Music | ✓ | ✓ | Bật |
| YouTube | ✓ | ✓ | Bật |
| SoundCloud | ✓ | ✓ | Bật |
| TikTok | ✓ | ✓ ¹ | Tắt |
| Instagram Reels | ✓ | ✓ ² | Tắt |
| Game (Liên Quân, Tốc Chiến, PUBG, Free Fire…) | — | ✓ | Bật |

<sub>¹ Trên điện thoại chỉ biết bạn đang lướt TikTok, không lấy được tên video. ² Trên điện thoại hiện khi mở Instagram, kể cả lúc nhắn tin.</sub>

- **Có tên bài, ảnh bìa, thời gian** — như Spotify.
- **Nhạc và game hiện cùng lúc.**
- **Cài là chạy** — không cần tạo ứng dụng Discord, không cần tài khoản gì thêm.
- **Bật / tắt từng loại** bằng công tắc; tắt một nút là ẩn hết. Điện thoại có ô cài đặt nhanh để ẩn bằng một chạm.
- **Chọn game nào được hiện** — game không muốn ai thấy thì bỏ tick.
- **Nhẹ**: chạy ẩn dưới khay, gần như không tốn CPU; điện thoại chỉ kiểm tra khi màn hình bật.
- **Riêng tư**: không đọc tin nhắn, màn hình hay dữ liệu trong game — chỉ biết app nào đang mở. Không đụng tới token Discord.
- **Tiếng Việt / English.**

> **Spotify?** Discord có sẵn: Cài đặt › **Kết nối** › **Spotify**. Không cần app này.

## Cài đặt

Tải `DiscordMobileStatus.exe` (máy tính) và `DiscordMobileStatus.apk` (điện thoại, tùy chọn) ở mục **Releases**.

**Máy tính**

1. Bấm đúp `DiscordMobileStatus.exe` (không cần cài). Windows cảnh báo thì bấm **More info › Run anyway**.
2. Mở Discord **app trên máy tính**, phát một bài trên trình duyệt. Xong.

Bấm **X** là app ẩn xuống khay. Chuột phải biểu tượng ở khay để bật/tắt nhanh hoặc **Thoát**. Bật **Chạy cùng Windows** nếu muốn tự chạy khi mở máy.

**Điện thoại Android** (để chia sẻ những gì bạn nghe / chơi trên điện thoại)

1. Cài APK. Play Protect chặn thì: Play Store › ảnh đại diện › Play Protect › ⚙ › tạm tắt **Quét ứng dụng**, cài xong bật lại.
2. Mở app, cấp 2 quyền theo nút trong app: **Truy cập thông báo** (nhạc / video) và **Truy cập dữ liệu sử dụng** (game, TikTok, Instagram). Bấm thêm **Không tối ưu pin**.
3. Trên máy tính: khối **Nguồn** › bật **Điện thoại Android** › dán **mã** hiện trong app điện thoại › **Lưu**.

> Trạng thái chỉ hiện khi máy tính đang mở Discord — đây là giới hạn của Discord.

## Gặp lỗi

| Hiện tượng | Làm gì |
|---|---|
| Discord không hiện gì | Dùng Discord **app máy tính** (không phải bản web) và bật Cài đặt › Quyền riêng tư hoạt động › **Hiển thị hoạt động hiện tại**. Xem các dòng trạng thái trong cửa sổ app. |
| TikTok / Instagram không hiện | Hai mục này **tắt sẵn** — bật trong khối Chia sẻ (cả máy tính lẫn điện thoại). |
| Công tắc quyền bị mờ (Android 13+) | Cài đặt › Ứng dụng › Discord Mobile Status › **⋮** › **Cho phép cài đặt bị hạn chế**. |
| Khóa màn hình một lúc thì ngừng | Tắt tối ưu pin, bật **Tự khởi chạy** cho app (Xiaomi, Oppo, Samsung). |
| Thoát game vẫn còn hiện | Đợi khoảng 30 giây. |
| Lỗi khác | Gửi file `DiscordMobileStatus.log` (nằm cạnh file exe) cho người chia sẻ app. |

---

<sub>Điện thoại gửi qua [ntfy.sh](https://ntfy.sh) bằng một mã riêng ngẫu nhiên — đừng đăng mã công khai. Tự build: `BUILD_EXE.bat`, `BUILD_APK.bat`. Tên cũ: YT Status.</sub>
