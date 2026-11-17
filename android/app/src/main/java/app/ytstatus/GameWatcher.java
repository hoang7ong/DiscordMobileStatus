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
    private static final long GRACE_MS = 2 * 60 * 1000;       // thoát ra chat / YouTube chưa tới 2 phút thì vẫn tính là đang chơi
    private static final long HEARTBEAT_MS = 10 * 60 * 1000;
    private static final long ICON_KEEP_MS = 150 * 60 * 1000;  // ntfy giữ ảnh 3 giờ

    static volatile String current = "";      // tên game đang chia sẻ (cho màn hình chính)

    private final Context c;
    private final Handler h = new Handler(Looper.getMainLooper());
    private UsageStatsManager usm;
    private boolean started, screenOn = true;

    private long lastQuery;          // mốc thời gian đã đọc sự kiện tới
    private String fg;               // app đang ở trước mặt
    private long fgSince;
    private String gamePkg, gameName;
    private long gameStart, leftAt, lastBeat, lastEnd;
    private String iconPkg, iconUrl;
    private long iconAt;

    GameWatcher(Context c) { this.c = c.getApplicationContext(); }

    private final Runnable tick = new Runnable() {
        @Override public void run() {
            if (!wanted()) { end(); return; }               // đang tắt -> không lên lịch lần sau
            poll();
            if (screenOn && started) h.postDelayed(this, POLL_MS);
        }
    };

    /** Có cần theo dõi game không (bật chia sẻ + bật Game + đã cấp quyền). */
    private boolean wanted() {
        return Prefs.enabled(c) && Prefs.games(c) && usm != null && Games.hasUsageAccess(c);
    }

    private final Runnable graceCheck = () -> {
        if (gamePkg != null && leftAt != 0 && System.currentTimeMillis() - leftAt >= GRACE_MS) end();
    };

    private final BroadcastReceiver screen = new BroadcastReceiver() {
        @Override public void onReceive(Context ctx, Intent i) {
            if (Intent.ACTION_SCREEN_OFF.equals(i.getAction())) {
                screenOn = false;
                h.removeCallbacks(tick);
                if (gamePkg != null && leftAt == 0) leftAt = System.currentTimeMillis();
                h.postDelayed(graceCheck, GRACE_MS + 1000);
            } else if (Intent.ACTION_SCREEN_ON.equals(i.getAction())) {
                screenOn = true;
                h.removeCallbacks(tick);
                if (wanted()) h.post(tick);
            }
        }
    };

    void start() {
        if (started) return;
        started = true;
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
                if (t == 1) {                                  // ACTIVITY_RESUMED (= MOVE_TO_FOREGROUND)
                    if (!e.getPackageName().equals(fg)) fgSince = e.getTimeStamp();
                    fg = e.getPackageName();
                } else if (t == 2 && e.getPackageName().equals(fg)) {   // ACTIVITY_PAUSED
                    fg = null;
                }
            }
        } catch (Exception ignored) {}
        lastQuery = now;

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
