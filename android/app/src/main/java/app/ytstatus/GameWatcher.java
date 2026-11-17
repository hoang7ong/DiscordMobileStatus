package app.ytstatus;

import android.app.usage.UsageEvents;
import android.app.usage.UsageStatsManager;
import android.content.BroadcastReceiver;
import android.content.Context;
import android.content.Intent;
import android.content.IntentFilter;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.drawable.Drawable;
import android.os.Handler;
import android.os.Looper;
import android.os.PowerManager;
import android.os.SystemClock;

/**
 * Biết game nào đang mở trên điện thoại (quyền "Truy cập dữ liệu sử dụng", giống Digital Wellbeing).
 * KHÔNG đọc màn hình hay bộ nhớ của game — chỉ biết tên app đang ở trước mặt.
 *
 * Tiết kiệm pin: chỉ kiểm tra khi màn hình bật, 15 giây một lần. Tắt màn hình thì ngừng hẳn.
 * Tiết kiệm lượt gửi: chỉ gửi khi vào / thoát game, và nhắc lại 10 phút một lần lúc đang chơi.
 */
final class GameWatcher {
    private static final long POLL_MS = 15000;
    private static final long GRACE_MS = 30 * 1000;           // rời game quá 30 giây -> ẩn (ra trả lời tin nhắn nhanh thì vẫn giữ)
    private static final long HEARTBEAT_MS = 5 * 60 * 1000;    // nhắc PC mỗi 5 phút (PC im 12 phút thì tự ẩn)
    private static final long LEFT_POLL_MS = 10000;            // vừa rời game: kiểm tra dày hơn để ẩn đúng hạn
    private static final long ICON_KEEP_MS = 150 * 60 * 1000;  // ntfy giữ ảnh 3 giờ

    static volatile String current = "";      // tên game đang chia sẻ (cho màn hình chính)
    /** Bộ dò đang làm gì: "run" đang theo dõi, "perm" chờ quyền, "off" đã tắt, "" chưa chạy. */
    static volatile String state = "";
    private static GameWatcher instance;
    private static final long PERM_RECHECK_MS = 30000;   // chưa có quyền: 30 giây xem lại 1 lần (chỉ khi màn hình bật)

    private final Context c;
    private final Handler h = new Handler(Looper.getMainLooper());
    private UsageStatsManager usm;
    private boolean started, screenOn = true;

    private long lastQuery;          // mốc thời gian đã đọc sự kiện tới
    private String fg, fgClass;      // app (và màn hình của app) đang ở trước mặt
    private long pausedAt;           // lần cuối app trước mặt bị tạm dừng
    private long fgSince;
    private String gamePkg, gameName;
    private long gameStart, leftAt, lastBeat, lastEnd;
    private String iconPkg, iconUrl;
    private long iconAt;

    GameWatcher(Context c) { this.c = c.getApplicationContext(); }

    private final Runnable tick = new Runnable() {
        @Override public void run() {
            if (!Prefs.enabled(c) || !Prefs.games(c) || usm == null) {   // người dùng tắt -> dừng hẳn, không lên lịch
                state = "off";
                end();
                return;
            }
            if (!Games.hasUsageAccess(c)) {
                // chưa cấp quyền: xem lại thưa thớt để cấp xong là tự chạy (không cần tắt/bật màn hình)
                state = "perm";
                end();
                if (screenOn && started) h.postDelayed(this, PERM_RECHECK_MS);
                return;
            }
            state = "run";
            poll();
            if (screenOn && started) h.postDelayed(this, leftAt != 0 ? LEFT_POLL_MS : POLL_MS);
        }
    };

    /** Màn hình chính gọi khi mở lại app (vd. vừa cấp quyền xong) -> kiểm tra ngay. */
    static void wake() {
        GameWatcher w = instance;
        if (w != null) w.poke();
    }

    /** Có cần theo dõi game không (bật chia sẻ + bật Game + đã cấp quyền). */
    private boolean wanted() {
        return Prefs.enabled(c) && Prefs.games(c) && usm != null && Games.hasUsageAccess(c);
    }


    private final BroadcastReceiver screen = new BroadcastReceiver() {
        @Override public void onReceive(Context ctx, Intent i) {
            if (Intent.ACTION_SCREEN_OFF.equals(i.getAction())) {
                screenOn = false;
                h.removeCallbacks(tick);
                // tắt màn hình = không còn chơi; ẩn ngay vì hẹn giờ của Android không chạy khi máy ngủ sâu
                end();
                fg = null;
                fgClass = null;
            } else if (Intent.ACTION_SCREEN_ON.equals(i.getAction())) {
                screenOn = true;
                h.removeCallbacks(tick);
                if (Prefs.enabled(c) && Prefs.games(c)) h.post(tick);
            }
        }
    };

    void start() {
        if (started) return;
        started = true;
        instance = this;
        usm = (UsageStatsManager) c.getSystemService(Context.USAGE_STATS_SERVICE);
        PowerManager pm = (PowerManager) c.getSystemService(Context.POWER_SERVICE);
        screenOn = pm == null || pm.isInteractive();
        IntentFilter f = new IntentFilter();
        f.addAction(Intent.ACTION_SCREEN_ON);
        f.addAction(Intent.ACTION_SCREEN_OFF);
        try { c.registerReceiver(screen, f); } catch (Exception ignored) {}
        h.post(tick);
    }

    void stop() {
        if (!started) return;
        started = false;
        if (instance == this) instance = null;
        state = "";
        try { c.unregisterReceiver(screen); } catch (Exception ignored) {}
        h.removeCallbacksAndMessages(null);
        end();
    }

    /** Cài đặt vừa đổi -> kiểm tra lại ngay. */
    void poke() {
        if (!started) return;
        h.removeCallbacks(tick);
        h.post(tick);
    }

    /** Nút "Gửi lại": gửi lại game đang chơi. */
    void resend() {
        if (!started) return;
        h.post(() -> { lastBeat = 0; iconUrl = null; poll(); });
    }

    private void poll() {
        long now = System.currentTimeMillis();
        if (lastQuery == 0) lastQuery = now - 10 * 60 * 1000;      // lần đầu: nhìn lại 10 phút để biết app đang mở
        try {
            UsageEvents ev = usm.queryEvents(lastQuery, now);
            UsageEvents.Event e = new UsageEvents.Event();
            while (ev != null && ev.hasNextEvent()) {
                ev.getNextEvent(e);
                int t = e.getEventType();
                String pkg = e.getPackageName();
                if (pkg == null) continue;
                if (t == 1) {                                  // ACTIVITY_RESUMED (= MOVE_TO_FOREGROUND)
                    if (!pkg.equals(fg)) fgSince = e.getTimeStamp();
                    fg = pkg;                                  // app nào lên trước mặt sau cùng = app đang dùng
                    fgClass = e.getClassName();
                    pausedAt = 0;                              // lên lại trước mặt -> bỏ dấu tạm dừng
                } else if ((t == 2 || t == 23) && pkg.equals(fg)) {   // ACTIVITY_PAUSED / ACTIVITY_STOPPED
                    pausedAt = e.getTimeStamp();               // ghi nhận; còn đang mở hay không do kiểm tra bên dưới
                }
            }
        } catch (Exception ignored) {}
        lastQuery = now;

        // Game đã bị tạm dừng (về màn hình chính, kéo thanh thông báo, mở app khác…) mà chưa thấy app nào khác lên:
        // hỏi Android trong 5 giây gần nhất app nào được dùng sau cùng. Nếu không phải game -> coi như đã rời game.
        if (fg != null && pausedAt >= fgSince && Games.shared(c, fg)) {
            String last = lastUsed(now);
            if (last == null || !last.equals(fg)) { fg = last; fgClass = null; fgSince = now; }
        }

        if (fg != null && Games.shared(c, fg)) {
            if (gamePkg == null || !gamePkg.equals(fg)) {
                if (gamePkg != null) end();                    // đổi thẳng sang game khác
                gamePkg = fg;
                gameName = Games.label(c, fg);
                gameStart = fgSince > lastEnd && fgSince > 0 ? fgSince : now;
                leftAt = 0;
                send(true);
            } else {
                leftAt = 0;                                    // quay lại game trong 2 phút -> vẫn ván cũ
                if (now - lastBeat >= HEARTBEAT_MS) send(false);
            }
        } else if (gamePkg != null) {
            if (leftAt == 0) leftAt = now;
            else if (now - leftAt >= GRACE_MS) end();
        }
    }

    /** App được đưa lên trước mặt gần nhất trong 2 phút vừa qua (null nếu không rõ / màn hình chính). */
    private String lastUsed(long now) {
        String last = null;
        try {
            UsageEvents ev = usm.queryEvents(now - 2 * 60 * 1000, now);
            UsageEvents.Event e = new UsageEvents.Event();
            String cur = null;
            while (ev != null && ev.hasNextEvent()) {
                ev.getNextEvent(e);
                int t = e.getEventType();
                String pkg = e.getPackageName();
                if (pkg == null) continue;
                if (t == 1) cur = pkg;
                else if ((t == 2 || t == 23) && pkg.equals(cur)) cur = null;
            }
            last = cur;
        } catch (Exception ignored) {}
        return last;
    }

    private void send(boolean newGame) {
        lastBeat = System.currentTimeMillis();
        current = gameName;
        boolean haveIcon = gamePkg.equals(iconPkg) && iconUrl != null && SystemClock.elapsedRealtime() - iconAt < ICON_KEEP_MS;
        Bitmap icon = haveIcon ? null : iconOf(gamePkg);       // icon chỉ tải lên 1 lần cho mỗi game (dùng lại 2,5 giờ)
        final String pkg = gamePkg;
        Sender.sendGame(c, pkg, gameName, gameStart, icon, haveIcon ? iconUrl : null, (ok, url) -> {
            if (ok && url != null) h.post(() -> {
                iconPkg = pkg;
                iconUrl = url;
                iconAt = SystemClock.elapsedRealtime();
            });
        });
    }

    private void end() {
        if (gamePkg != null) Sender.sendGameStop(c);
        gamePkg = null;
        gameName = null;
        leftAt = 0;
        lastEnd = System.currentTimeMillis();
        current = "";
    }

    /** Biểu tượng app -> ảnh 128 px. */
    private Bitmap iconOf(String pkg) {
        try {
            Drawable d = c.getPackageManager().getApplicationIcon(pkg);
            Bitmap b = Bitmap.createBitmap(128, 128, Bitmap.Config.ARGB_8888);
            Canvas cv = new Canvas(b);
            d.setBounds(0, 0, 128, 128);
            d.draw(cv);
            return b;
        } catch (Exception e) {
            return null;
        }
    }
}
