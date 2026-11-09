package app.ytstatus;

import android.content.Context;
import android.graphics.Bitmap;

import org.json.JSONObject;

import java.io.ByteArrayOutputStream;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.HttpURLConnection;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/**
 * Gửi bài đang nghe lên ntfy.sh (dịch vụ chuyển tin miễn phí, không cần tài khoản).
 * App trên PC theo dõi cùng "mã kênh" sẽ nhận được ngay.
 * Mỗi tin là một dòng JSON ngắn; ảnh bìa đi kèm dạng tệp đính kèm (ntfy giữ 3 giờ).
 */
final class Sender {
    interface Done { void result(boolean ok, String artUrl); }

    private static final ExecutorService EX = Executors.newSingleThreadExecutor();

    static volatile String lastStatus = null;   // null = chưa gửi gì
    static volatile String lastTrack = "";

    private Sender() {}

    static final class Snap {
        String title = "", artist = "", album = "", app = "ytmusic", mediaId = "";
        long durationMs, positionMs;
        boolean playing;
    }

    static JSONObject toJson(Snap s, String artUrl) throws Exception {
        JSONObject j = new JSONObject();
        j.put("v", 1);
        j.put("t", s.title);
        j.put("a", s.artist);
        if (!s.album.isEmpty()) j.put("al", s.album);
        j.put("app", s.app);
        if (!s.mediaId.isEmpty()) j.put("id", s.mediaId);
        j.put("d", s.durationMs);
        j.put("p", s.positionMs);
        j.put("pl", s.playing);
        j.put("ts", System.currentTimeMillis());
        if (artUrl != null && !artUrl.isEmpty()) j.put("art", artUrl);
        return j;
    }

    /** art != null -> gửi kèm ảnh bìa; callback trả URL ảnh (để lần sau dùng lại, khỏi gửi ảnh nữa). */
    static void sendTrack(Context ctx, Snap s, Bitmap art, String knownArtUrl, Done done) {
        final Context c = ctx.getApplicationContext();
        final byte[] jpeg = art == null ? null : jpeg(art);
        EX.execute(() -> {
            String artUrl = knownArtUrl;
            String err = null;
            try {
                JSONObject msg = toJson(s, artUrl);
                if (jpeg != null) {
                    try {
                        JSONObject r = upload(c, msg.toString(), jpeg);
                        JSONObject att = r.optJSONObject("attachment");
                        if (att != null) artUrl = att.optString("url", null);
                    } catch (HttpError e) {
                        if (e.code == 429) throw e;
                        // tệp đính kèm bị từ chối -> gửi chữ thôi, PC tự tìm ảnh bìa
                        post(c, msg.toString());
                    }
                } else {
                    post(c, msg.toString());
                }
            } catch (HttpError e) {
                err = e.code == 429 ? Prefs.t(c, "ntfy.sh báo hết lượt hôm nay (250 tin/ngày). Mai tự chạy lại.",
                        "ntfy.sh daily limit reached (250 messages/day). It resumes tomorrow.") : Prefs.t(c, "Lỗi HTTP ", "HTTP error ") + e.code;
            } catch (Exception e) {
                err = Prefs.t(c, "Lỗi mạng: ", "Network error: ") + e.getMessage();
            }
            lastTrack = s.title + (s.artist.isEmpty() ? "" : " — " + s.artist) + (s.playing ? "" : Prefs.t(c, "  (tạm dừng)", "  (paused)"));
            lastStatus = err == null ? Prefs.t(c, "Đã gửi lúc ", "Sent at ") + clock() : err + " (" + clock() + ")";
            if (done != null) done.result(err == null, artUrl);
        });
    }

    static void sendStop(Context ctx) {
        final Context c = ctx.getApplicationContext();
        EX.execute(() -> {
            try {
                post(c, "{\"v\":1,\"stop\":true}");
                lastStatus = Prefs.t(c, "Đã ẩn lúc ", "Hidden at ") + clock();
            } catch (Exception e) {
                lastStatus = Prefs.t(c, "Lỗi mạng lúc ", "Network error at ") + clock() + ": " + e.getMessage();
            }
            lastTrack = "";
        });
    }

    // ---------------------------------------------------------------- nội bộ
    static final class HttpError extends Exception {
        final int code;
        HttpError(int code) { super("HTTP " + code); this.code = code; }
    }

    private static String topicUrl(Context c) {
        return Prefs.server(c) + "/" + Prefs.channel(c);
    }

    private static JSONObject post(Context c, String text) throws Exception {
        byte[] b = text.getBytes(StandardCharsets.UTF_8);
        return request(topicUrl(c), "POST", b, "text/plain; charset=utf-8", null);
    }

    private static JSONObject upload(Context c, String text, byte[] jpeg) throws Exception {
        String url = topicUrl(c) + "?message=" + URLEncoder.encode(text, "UTF-8");
        return request(url, "PUT", jpeg, "image/jpeg", "cover.jpg");
    }

    private static JSONObject request(String url, String method, byte[] body, String ctype, String filename) throws Exception {
        HttpURLConnection h = (HttpURLConnection) new URL(url).openConnection();
        try {
            h.setConnectTimeout(12000);
            h.setReadTimeout(20000);
            h.setRequestMethod(method);
            h.setRequestProperty("Content-Type", ctype);
            h.setRequestProperty("Priority", "1");          // tin "rất thấp", không rung nếu ai đó lỡ đăng ký kênh
            if (filename != null) h.setRequestProperty("Filename", filename);
            h.setDoOutput(true);
            h.setFixedLengthStreamingMode(body.length);
            try (OutputStream o = h.getOutputStream()) { o.write(body); }
            int code = h.getResponseCode();
            if (code >= 400) throw new HttpError(code);
            try (InputStream in = h.getInputStream()) {
                return new JSONObject(readAll(in));
            } catch (Exception e) {
                return new JSONObject();
            }
        } finally {
            h.disconnect();
        }
    }

    private static String readAll(InputStream in) throws Exception {
        ByteArrayOutputStream o = new ByteArrayOutputStream();
        byte[] buf = new byte[8192];
        int n;
        while ((n = in.read(buf)) > 0) o.write(buf, 0, n);
        return new String(o.toByteArray(), StandardCharsets.UTF_8);
    }

    /** Thu ảnh bìa về tối đa 320px, JPEG ~20 KB. */
    private static byte[] jpeg(Bitmap bm) {
        if (bm == null || bm.isRecycled() || bm.getWidth() < 8 || bm.getHeight() < 8) return null;
        try {
            int w = bm.getWidth(), hgt = bm.getHeight();
            float k = Math.min(1f, 320f / Math.max(w, hgt));
            Bitmap small = k < 1f ? Bitmap.createScaledBitmap(bm, Math.max(1, Math.round(w * k)), Math.max(1, Math.round(hgt * k)), true) : bm;
            ByteArrayOutputStream o = new ByteArrayOutputStream();
            small.compress(Bitmap.CompressFormat.JPEG, 85, o);
            return o.toByteArray();
        } catch (Exception e) {
            return null;
        }
    }

    private static String clock() {
        return new java.text.SimpleDateFormat("HH:mm:ss", java.util.Locale.ROOT).format(new java.util.Date());
    }
}
