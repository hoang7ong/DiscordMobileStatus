package app.ytstatus;

import android.animation.LayoutTransition;
import android.app.Activity;
import android.app.AlertDialog;
import android.content.ClipData;
import android.content.ClipboardManager;
import android.content.ComponentName;
import android.content.Intent;
import android.content.res.ColorStateList;
import android.graphics.Color;
import android.graphics.Typeface;
import android.graphics.drawable.GradientDrawable;
import android.net.Uri;
import android.os.Build;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.os.PowerManager;
import android.provider.Settings;
import android.service.notification.NotificationListenerService;
import android.text.InputType;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;
import android.widget.CompoundButton;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.Switch;
import android.widget.TextView;
import android.widget.Toast;

/**
 * Màn hình duy nhất, dựng bằng code (không XML, không thư viện) theo phong cách Once UI:
 * nền tối trung tính, khối bo góc, công tắc màu nhấn, khối con mở/thu gọn mượt (LayoutTransition).
 *
 * Hiệu năng: chỉ làm mới mỗi 1,5 giây khi đang mở app (dừng hẳn khi rời app), và chỉ đổi chữ
 * khi nội dung thật sự khác -> không vẽ lại thừa.
 */
public class MainActivity extends Activity {
    private static final int BG = 0xFF0E0F12, CARD = 0xFF17191D, LINE = 0xFF2A2D34, HI = 0xFF1E2025;
    private static final int TEXT = 0xFFEDEEF0, MUTED = 0xFF9297A0, FAINT = 0xFF62676F;
    private static final int BRAND = 0xFF5865F2, GREEN = 0xFF3BD671, RED = 0xFFF23F43, TRACK = 0xFF3A3D45;

    private final Handler h = new Handler(Looper.getMainLooper());
    private TextView nowText, statusText, gameText, codeText, permState, usageState;
    private Button permBtn, usageBtn;
    private LinearLayout shareBody, gameBody;
    private float dp;

    private final Runnable tick = new Runnable() {
        @Override public void run() { refresh(false); h.postDelayed(this, 1500); }
    };

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        dp = getResources().getDisplayMetrics().density;
        getWindow().setStatusBarColor(BG);
        getWindow().setNavigationBarColor(BG);

        ScrollView sv = new ScrollView(this);
        sv.setBackgroundColor(BG);
        sv.setFillViewport(true);
        sv.setVerticalScrollBarEnabled(false);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(px(18), px(24), px(18), px(28));
        root.setLayoutTransition(smooth());
        sv.addView(root);
        setContentView(sv);

        // ---- tiêu đề + chọn ngôn ngữ
        LinearLayout head = new LinearLayout(this);
        head.setOrientation(LinearLayout.HORIZONTAL);
        head.setGravity(Gravity.CENTER_VERTICAL);
        head.addView(label("Discord Mobile Status", 21, TEXT, true), weight(0));
        Button lang = pill(Prefs.en(this) ? "Tiếng Việt" : "English");
        lang.setOnClickListener(v -> {
            Prefs.setEn(this, !Prefs.en(this));
            ToggleTile.refresh(this);
            recreate();
        });
        head.addView(lang, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        root.addView(head);
        TextView sub = label(tr("Nhạc, video và game trên điện thoại → trạng thái Discord.", "Music, videos and games on your phone → your Discord status."), 13, MUTED, false);
        sub.setPadding(0, px(4), 0, px(16));
        root.addView(sub);

        // ---- khối Đang gửi
        LinearLayout now = card(tr("Đang gửi", "Sending"));
        nowText = label("—", 16, TEXT, true);
        now.addView(nowText);
        statusText = label("", 12, MUTED, false);
        statusText.setPadding(0, px(3), 0, 0);
        now.addView(statusText);
        gameText = label("", 14, TEXT, false);
        gameText.setPadding(0, px(10), 0, 0);
        now.addView(gameText);
        Button resend = button(tr("Gửi lại ngay", "Resend now"), false);
        resend.setOnClickListener(v -> {
            if (!NowListener.forceSend()) toast(tr("Dịch vụ chưa chạy — cấp quyền ở khối Quyền.", "Service not running — grant the permission below."));
        });
        now.addView(resend, matchWrap(px(12)));
        root.addView(now);

        // ---- khối Chia sẻ: công tắc chính, tắt thì ẩn hết bên dưới
        LinearLayout share = card(tr("Chia sẻ", "Sharing"));
        shareBody = vbox();
        Switch master = toggle(tr("Chia sẻ lên Discord", "Share on Discord"), tr("Tắt để ẩn hết khỏi trạng thái Discord", "Turn off to hide everything"),
                "enabled", Prefs.enabled(this), share);
        master.setOnCheckedChangeListener((CompoundButton bb, boolean on) -> {
            Prefs.put(this, "enabled", on);
            ToggleTile.refresh(this);
            shareBody.setVisibility(on ? View.VISIBLE : View.GONE);
            refresh(true);
        });
        shareBody.addView(divider());
        toggle("YouTube Music", tr("Đang nghe…", "Listening to…"), "ytmusic", Prefs.ytmusic(this), shareBody);
        toggle("YouTube", tr("Đang xem…", "Watching…"), "youtube", Prefs.youtube(this), shareBody);
        toggle("SoundCloud", tr("Đang nghe…", "Listening to…"), "soundcloud", Prefs.soundcloud(this), shareBody);
        Switch gsw = toggle(tr("Game", "Games"), tr("Đang chơi… (tên game + thời gian)", "Playing… (game name + time)"),
                "games", Prefs.games(this), shareBody);
        gameBody = vbox();
        Button pick = button(tr("Chọn game được chia sẻ…", "Choose which games to share…"), false);
        pick.setOnClickListener(v -> pickGames());
        gameBody.addView(pick, matchWrap(px(4)));
        shareBody.addView(gameBody);
        gsw.setOnCheckedChangeListener((CompoundButton bb, boolean on) -> {
            Prefs.put(this, "games", on);
            gameBody.setVisibility(on ? View.VISIBLE : View.GONE);
            refresh(true);
        });
        shareBody.addView(divider());
        toggle("TikTok", tr("Đang lướt TikTok (cần quyền xem app đang mở)", "Scrolling TikTok (needs usage access)"),
                "tiktok", Prefs.tiktok(this), shareBody);
        toggle("Instagram", tr("Hiện khi mở Instagram — kể cả lúc nhắn tin, không chỉ Reels", "Shows whenever Instagram is open — DMs too, not just Reels"),
                "instagram", Prefs.instagram(this), shareBody);
        gameBody.setVisibility(Prefs.games(this) ? View.VISIBLE : View.GONE);
        share.addView(shareBody);
        shareBody.setVisibility(Prefs.enabled(this) ? View.VISIBLE : View.GONE);
        TextView qs = label(tr("Mẹo: thêm ô \"Discord Status\" vào cài đặt nhanh để bật/tắt bằng một chạm.",
                "Tip: add the \"Discord Status\" quick-settings tile to toggle with one tap."), 12, FAINT, false);
        qs.setPadding(0, px(10), 0, 0);
        share.addView(qs);
        root.addView(share);

        // ---- khối Quyền
        LinearLayout perm = card(tr("Quyền", "Permissions"));
        permState = label("", 14, TEXT, false);
        perm.addView(permState);
        permBtn = button(tr("Mở cài đặt truy cập thông báo", "Open notification access settings"), true);
        permBtn.setOnClickListener(v -> openListenerSettings());
        perm.addView(permBtn, matchWrap(px(10)));
        TextView hint = label(tr("Công tắc bị mờ (Android 13+): Cài đặt › Ứng dụng › Discord Mobile Status › ⋮ › \"Cho phép cài đặt bị hạn chế\".",
                "Switch greyed out (Android 13+): Settings › Apps › Discord Mobile Status › ⋮ › \"Allow restricted settings\"."), 12, FAINT, false);
        hint.setPadding(0, px(6), 0, 0);
        perm.addView(hint);
        perm.addView(divider());
        usageState = label("", 14, TEXT, false);
        perm.addView(usageState);
        TextView uh = label(tr("Để biết game nào đang mở (giống Digital Wellbeing). Không đọc màn hình hay dữ liệu trong game.",
                "To know which game is open (like Digital Wellbeing). Never reads the screen or in-game data."), 12, FAINT, false);
        uh.setPadding(0, px(4), 0, 0);
        perm.addView(uh);
        usageBtn = button(tr("Mở cài đặt truy cập dữ liệu sử dụng", "Open usage access settings"), true);
        usageBtn.setOnClickListener(v -> openUsageSettings());
        perm.addView(usageBtn, matchWrap(px(10)));
        Button battery = button(tr("Không tối ưu pin (chạy ngầm ổn định)", "Disable battery optimization"), false);
        battery.setOnClickListener(v -> askBattery());
        perm.addView(battery, matchWrap(px(8)));
        root.addView(perm);

        // ---- khối Mã
        LinearLayout code = card(tr("Mã — nhập vào máy tính", "Code — enter it on your PC"));
        codeText = label(Prefs.channel(this), 23, TEXT, true);
        codeText.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        codeText.setGravity(Gravity.CENTER);
        codeText.setPadding(0, px(8), 0, px(8));
        code.addView(codeText, matchWrap(0));
        TextView ch = label(tr("Mã riêng để máy tính nhận từ điện thoại. Đừng đăng công khai.",
                "Your private code for the PC app. Don't post it publicly."), 12, FAINT, false);
        code.addView(ch);
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        Button copy = button(tr("Sao chép", "Copy"), true);
        copy.setOnClickListener(v -> copyCode());
        Button send = button(tr("Gửi sang máy tính", "Send to PC"), false);
        send.setOnClickListener(v -> shareCode());
        row.addView(copy, weight(0));
        row.addView(send, weight(px(8)));
        code.addView(row, matchWrap(px(12)));
        Button adv = button(tr("Đổi mã / đổi server…", "Change code / server…"), false);
        adv.setOnClickListener(v -> advanced());
        code.addView(adv, matchWrap(px(8)));
        root.addView(code);
    }

    @Override protected void onResume() {
        super.onResume();
        if (listenerAllowed() && !NowListener.connected && Build.VERSION.SDK_INT >= 24) {
            try { NotificationListenerService.requestRebind(new ComponentName(this, NowListener.class)); } catch (Exception ignored) {}
        }
        refresh(true);                  // kiểm tra quyền ngay (vừa cấp xong trong Cài đặt)
        GameWatcher.wake();             // ...và cho bộ dò game chạy luôn
        h.postDelayed(tick, 1500);
    }

    @Override protected void onPause() {
        h.removeCallbacks(tick);        // rời app -> không làm mới gì nữa
        super.onPause();
    }

    /** Làm mới chữ trên màn hình. fresh=true: kiểm tra quyền ngay (không dùng kết quả nhớ). */
    private void refresh(boolean fresh) {
        boolean ok = listenerAllowed();
        set(permState, ok ? (NowListener.connected ? tr("● Truy cập thông báo: đã cấp, đang chạy ngầm", "● Notification access: granted, running")
                : tr("● Truy cập thông báo: đã cấp, đang khởi động…", "● Notification access: granted, starting…"))
                : tr("● Chưa cấp truy cập thông báo", "● Notification access not granted"), ok ? GREEN : RED);
        vis(permBtn, !ok);
        boolean ua = Games.hasUsageAccess(this, fresh);
        set(usageState, ua ? tr("● Xem game đang mở: đã cấp", "● Game detection: allowed")
                : tr("● Xem game đang mở: chưa cấp (tuỳ chọn)", "● Game detection: not allowed (optional)"), ua ? GREEN : MUTED);
        vis(usageBtn, !ua);

        String t = Sender.lastTrack;
        if (!Prefs.enabled(this)) t = tr("Đang tắt chia sẻ", "Sharing is off");
        set(nowText, t == null || t.isEmpty() ? tr("Không phát gì", "Nothing playing") : t, TEXT);
        set(statusText, Sender.lastStatus == null ? tr("Chưa gửi gì.", "Nothing sent yet.") : Sender.lastStatus, MUTED);
        String g = GameWatcher.current;
        String st = GameWatcher.state;
        String gl;
        boolean playing = g != null && !g.isEmpty();
        if (!Prefs.watchApps(this) || !Prefs.enabled(this)) gl = tr("Game / TikTok / Instagram: đang tắt", "Games / TikTok / Instagram: off");
        else if (!ua) gl = tr("Game / TikTok / Instagram: chưa cấp quyền xem app đang mở", "Games / TikTok / Instagram: usage access not granted");
        else if (!listenerAllowed() || "".equals(st)) gl = tr("Chưa chạy — cần quyền truy cập thông báo", "Not running — needs notification access");
        else if (playing) gl = ("TikTok".equals(g) || "Instagram".equals(g) ? tr("Đang lướt: ", "Scrolling: ") : tr("Đang chơi: ", "Playing: ")) + g;
        else gl = tr("Đang theo dõi, chưa mở game / TikTok / Instagram", "Watching, no game / TikTok / Instagram open");
        String gs = Sender.lastGameStatus;
        if (playing && gs != null) gl += "\n" + gs;          // "Đã gửi lúc …" hoặc lỗi mạng / hết lượt
        set(gameText, gl, playing ? GREEN : MUTED);
    }

    /** Chỉ đổi chữ / màu khi khác (tránh đo + vẽ lại thừa). */
    private static void set(TextView v, String s, int color) {
        if (!s.contentEquals(v.getText())) v.setText(s);
        if (v.getCurrentTextColor() != color) v.setTextColor(color);
    }

    private static void vis(View v, boolean show) {
        int want = show ? View.VISIBLE : View.GONE;
        if (v.getVisibility() != want) v.setVisibility(want);
    }

    private void copyCode() {
        ClipboardManager cm = (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
        cm.setPrimaryClip(ClipData.newPlainText("Discord Mobile Status", Prefs.channel(this)));
        toast(tr("Đã sao chép mã.", "Code copied."));
    }

    private void shareCode() {
        Intent i = new Intent(Intent.ACTION_SEND);
        i.setType("text/plain");
        i.putExtra(Intent.EXTRA_TEXT, Prefs.channel(this));
        startActivity(Intent.createChooser(i, tr("Gửi mã sang máy tính (Zalo, email…)", "Send the code to your PC (email, chat…)")));
    }

    private void advanced() {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.setPadding(px(20), px(8), px(20), 0);
        box.addView(label(tr("Server (để trống = ntfy.sh). Chỉ đổi nếu bạn tự dựng server ntfy riêng.",
                "Server (empty = ntfy.sh). Only change it if you run your own ntfy server."), 13, MUTED, false));
        EditText srv = new EditText(this);
        srv.setHint(Prefs.DEFAULT_SERVER);
        srv.setSingleLine(true);
        srv.setInputType(InputType.TYPE_TEXT_VARIATION_URI);
        srv.setText(Prefs.server(this));
        box.addView(srv, matchWrap(px(8)));
        new AlertDialog.Builder(this)
                .setTitle(tr("Nâng cao", "Advanced"))
                .setView(box)
                .setPositiveButton(tr("Lưu", "Save"), (d, w) -> {
                    Prefs.put(this, "server", srv.getText().toString());
                    NowListener.forceSend();
                    toast(tr("Đã lưu.", "Saved."));
                })
                .setNeutralButton(tr("Tạo mã mới", "New code"), (d, w) -> {
                    codeText.setText(Prefs.newChannel(this));
                    NowListener.forceSend();
                    toast(tr("Đã tạo mã mới — nhập lại vào máy tính.", "New code created — enter it on your PC again."));
                })
                .setNegativeButton(tr("Huỷ", "Cancel"), null)
                .show();
    }

    private boolean listenerAllowed() {
        String flat = Settings.Secure.getString(getContentResolver(), "enabled_notification_listeners");
        return flat != null && flat.contains(getPackageName() + "/");
    }

    private void openListenerSettings() {
        try {
            if (Build.VERSION.SDK_INT >= 30) {
                Intent i = new Intent("android.settings.NOTIFICATION_LISTENER_DETAIL_SETTINGS");
                i.putExtra("android.provider.extra.NOTIFICATION_LISTENER_COMPONENT_NAME",
                        new ComponentName(this, NowListener.class).flattenToString());
                startActivity(i);
                return;
            }
        } catch (Exception ignored) {}
        startActivity(new Intent("android.settings.ACTION_NOTIFICATION_LISTENER_SETTINGS"));
    }

    private void openUsageSettings() {
        try {
            Intent i = new Intent(Settings.ACTION_USAGE_ACCESS_SETTINGS);
            if (Build.VERSION.SDK_INT >= 29) i.setData(Uri.parse("package:" + getPackageName()));
            startActivity(i);
        } catch (Exception e) {
            try { startActivity(new Intent(Settings.ACTION_USAGE_ACCESS_SETTINGS)); } catch (Exception ignored) {}
        }
    }

    /** Danh sách app: game (tự nhận) bật sẵn, app khác tắt sẵn; tick để chia sẻ. Đọc danh sách trên luồng nền. */
    private void pickGames() {
        toast(tr("Đang lấy danh sách app…", "Loading apps…"));
        Thread th = new Thread(() -> {
            final java.util.List<Games.App> apps = Games.launcherApps(this);
            h.post(() -> {
                if (isFinishing()) return;
                final java.util.Set<String> off = Games.off(this);
                final java.util.Set<String> extra = Games.extra(this);
                String[] names = new String[apps.size()];
                boolean[] checked = new boolean[apps.size()];
                for (int i = 0; i < apps.size(); i++) {
                    Games.App a = apps.get(i);
                    names[i] = a.label + (a.game ? "  🎮" : "");
                    checked[i] = a.game ? !off.contains(a.pkg) : extra.contains(a.pkg);
                }
                new AlertDialog.Builder(this)
                        .setTitle(tr("Chia sẻ những app nào", "Share which apps"))
                        .setMultiChoiceItems(names, checked, (d, which, on) -> checked[which] = on)
                        .setPositiveButton(tr("Lưu", "Save"), (d, w) -> {
                            java.util.Set<String> newOff = new java.util.HashSet<>();
                            java.util.Set<String> newExtra = new java.util.HashSet<>();
                            for (int i = 0; i < apps.size(); i++) {
                                Games.App a = apps.get(i);
                                if (a.game && !checked[i]) newOff.add(a.pkg);
                                if (!a.game && checked[i]) newExtra.add(a.pkg);
                            }
                            Prefs.sp(this).edit().putStringSet("game_off", newOff).putStringSet("game_extra", newExtra).apply();
                            toast(tr("Đã lưu.", "Saved."));
                        })
                        .setNegativeButton(tr("Huỷ", "Cancel"), null)
                        .show();
            });
        }, "dms-apps");
        th.setPriority(Thread.MIN_PRIORITY);
        th.start();
    }

    private void askBattery() {
        PowerManager pm = (PowerManager) getSystemService(POWER_SERVICE);
        if (pm != null && pm.isIgnoringBatteryOptimizations(getPackageName())) {
            toast(tr("Đã tắt tối ưu pin cho app rồi.", "Battery optimization is already off for this app."));
            return;
        }
        try {
            Intent i = new Intent(Settings.ACTION_REQUEST_IGNORE_BATTERY_OPTIMIZATIONS);
            i.setData(Uri.parse("package:" + getPackageName()));
            startActivity(i);
        } catch (Exception e) {
            startActivity(new Intent(Settings.ACTION_IGNORE_BATTERY_OPTIMIZATION_SETTINGS));
        }
    }

    // ------------------------------------------------------------- dựng giao diện
    private int px(int v) { return Math.round(v * dp); }

    private String tr(String vi, String en) { return Prefs.t(this, vi, en); }

    /** Hiệu ứng mở / thu gọn mượt khi một khối con hiện ra hoặc ẩn đi. */
    private static LayoutTransition smooth() {
        LayoutTransition lt = new LayoutTransition();
        lt.enableTransitionType(LayoutTransition.CHANGING);
        lt.setDuration(180);
        return lt;
    }

    private TextView label(String s, int sp, int color, boolean bold) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(sp);
        t.setTextColor(color);
        if (bold) t.setTypeface(Typeface.DEFAULT_BOLD);
        t.setLineSpacing(0, 1.15f);
        return t;
    }

    private LinearLayout vbox() {
        LinearLayout v = new LinearLayout(this);
        v.setOrientation(LinearLayout.VERTICAL);
        v.setLayoutTransition(smooth());
        return v;
    }

    /** Khối: nền tối hơn trang, viền mảnh, bo góc 18, tiêu đề chữ nhỏ in hoa. */
    private LinearLayout card(String caption) {
        LinearLayout c = vbox();
        c.setPadding(px(16), px(14), px(16), px(16));
        GradientDrawable g = new GradientDrawable();
        g.setColor(CARD);
        g.setCornerRadius(px(18));
        g.setStroke(px(1), LINE);
        c.setBackground(g);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = px(12);
        c.setLayoutParams(lp);
        TextView t = label(caption.toUpperCase(java.util.Locale.ROOT), 11, MUTED, true);
        t.setLetterSpacing(0.08f);
        t.setPadding(0, 0, 0, px(10));
        c.addView(t);
        return c;
    }

    private View divider() {
        View v = new View(this);
        v.setBackgroundColor(LINE);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, Math.max(1, px(1) / 2 + 1));
        lp.topMargin = px(8);
        lp.bottomMargin = px(8);
        v.setLayoutParams(lp);
        return v;
    }

    private Button button(String s, boolean primary) {
        Button b = new Button(this);
        b.setText(s);
        b.setAllCaps(false);
        b.setTextSize(15);
        b.setTextColor(primary ? Color.WHITE : TEXT);
        b.setGravity(Gravity.CENTER);       // chữ căn giữa tuyệt đối
        b.setIncludeFontPadding(false);
        b.setMinHeight(px(46));
        b.setMinimumHeight(px(46));
        b.setPadding(px(12), 0, px(12), 0);
        b.setStateListAnimator(null);
        GradientDrawable g = new GradientDrawable();
        g.setColor(primary ? BRAND : HI);
        g.setCornerRadius(px(12));
        if (!primary) g.setStroke(px(1), LINE);
        b.setBackground(g);
        return b;
    }

    /** Nút viên thuốc nhỏ (chọn ngôn ngữ). */
    private Button pill(String s) {
        Button b = button(s, false);
        b.setTextSize(13);
        b.setMinHeight(px(34));
        b.setMinimumHeight(px(34));
        b.setPadding(px(14), 0, px(14), 0);
        GradientDrawable g = new GradientDrawable();
        g.setColor(CARD);
        g.setCornerRadius(px(17));
        g.setStroke(px(1), LINE);
        b.setBackground(g);
        return b;
    }

    /** Hàng công tắc: tiêu đề + mô tả nhỏ. Mặc định lưu ngay vào key; listener riêng thì gán đè sau. */
    private Switch toggle(String title, String desc, String key, boolean value, LinearLayout parent) {
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.setPadding(0, px(6), 0, px(6));
        LinearLayout texts = new LinearLayout(this);
        texts.setOrientation(LinearLayout.VERTICAL);
        texts.addView(label(title, 15, TEXT, false));
        if (desc != null) texts.addView(label(desc, 12, MUTED, false));
        row.addView(texts, weight(0));
        Switch s = new Switch(this);
        s.setChecked(value);
        int[][] states = { { android.R.attr.state_checked }, {} };
        s.setThumbTintList(new ColorStateList(states, new int[] { Color.WHITE, 0xFFC9CCD2 }));
        s.setTrackTintList(new ColorStateList(states, new int[] { BRAND, TRACK }));
        s.setOnCheckedChangeListener((CompoundButton b, boolean on) -> Prefs.put(this, key, on));
        row.addView(s, new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT));
        row.setOnClickListener(v -> s.toggle());        // bấm cả hàng cũng bật/tắt
        parent.addView(row, matchWrap(0));
        return s;
    }

    private LinearLayout.LayoutParams matchWrap(int top) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.topMargin = top;
        return lp;
    }

    private LinearLayout.LayoutParams weight(int left) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, ViewGroup.LayoutParams.WRAP_CONTENT, 1f);
        lp.leftMargin = left;
        return lp;
    }

    private void toast(String s) { Toast.makeText(this, s, Toast.LENGTH_SHORT).show(); }
}
