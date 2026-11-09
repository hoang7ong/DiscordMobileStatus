package app.ytstatus;

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

/** Màn hình duy nhất: cấp quyền, xem mã kênh, bật/tắt. Giao diện dựng bằng code. */
public class MainActivity extends Activity {
    private static final int BG = 0xFF101114, CARD = 0xFF1B1D22, LINE = 0xFF2A2D34;
    private static final int TEXT = 0xFFF2F3F5, MUTED = 0xFF9AA0AA, RED = 0xFFFF2D3F, GREEN = 0xFF3BD671;

    private final Handler h = new Handler(Looper.getMainLooper());
    private TextView permState, nowText, statusText, codeText;
    private Button permBtn;
    private float dp;

    private final Runnable tick = new Runnable() {
        @Override public void run() { refresh(); h.postDelayed(this, 1500); }
    };

    @Override protected void onCreate(Bundle b) {
        super.onCreate(b);
        dp = getResources().getDisplayMetrics().density;
        getWindow().setStatusBarColor(BG);
        getWindow().setNavigationBarColor(BG);

        ScrollView sv = new ScrollView(this);
        sv.setBackgroundColor(BG);
        sv.setFillViewport(true);
        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setPadding(px(20), px(28), px(20), px(28));
        sv.addView(root);
        setContentView(sv);

        // tiêu đề + nút đổi ngôn ngữ
        LinearLayout head = new LinearLayout(this);
        head.setOrientation(LinearLayout.HORIZONTAL);
        head.setGravity(Gravity.CENTER_VERTICAL);
        head.addView(label("YT Status", 26, TEXT, true), weight(0));
        Button lang = button(Prefs.en(this) ? "Tiếng Việt" : "English", false);
        lang.setTextSize(13);
        lang.setMinHeight(px(36));
        lang.setMinimumHeight(px(36));
        lang.setPadding(px(14), 0, px(14), 0);
        lang.setOnClickListener(v -> {
            Prefs.setEn(this, !Prefs.en(this));
            ToggleTile.refresh(this);
            recreate();
        });
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.WRAP_CONTENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        head.addView(lang, lp);
        root.addView(head);
        TextView sub = label(tr("Hiện bạn đang nghe / xem gì trên YouTube, YouTube Music lên trạng thái Discord.", "Shows what you play on YouTube and YouTube Music as your Discord status."), 14, MUTED, false);
        sub.setPadding(0, px(4), 0, px(16));
        root.addView(sub);

        // --- đang phát
        LinearLayout now = card();
        now.addView(caption(tr("ĐANG GỬI", "SENDING")));
        nowText = label("—", 17, TEXT, true);
        now.addView(nowText);
        statusText = label("", 13, MUTED, false);
        statusText.setPadding(0, px(4), 0, 0);
        now.addView(statusText);
        Button resend = button(tr("Gửi lại ngay", "Resend now"), false);
        resend.setOnClickListener(v -> {
            if (!NowListener.forceSend()) toast(tr("Dịch vụ chưa chạy — kiểm tra quyền ở bước 1.", "Service not running — check the permission in step 1."));
        });
        now.addView(resend, matchWrap(px(12)));
        root.addView(now);

        // --- bước 1: quyền
        LinearLayout perm = card();
        perm.addView(caption(tr("BƯỚC 1 · QUYỀN", "STEP 1 · PERMISSION")));
        permState = label("", 15, TEXT, false);
        perm.addView(permState);
        TextView hint = label(tr("Android 13 trở lên: nếu công tắc bị mờ, vào Cài đặt › Ứng dụng › YT Status › dấu ⋮ góc trên › "
                + "\"Cho phép cài đặt bị hạn chế\", rồi quay lại bật.",
                "Android 13+: if the switch is greyed out, open Settings › Apps › YT Status › ⋮ (top right) › "
                + "\"Allow restricted settings\", then come back and turn it on."), 12, MUTED, false);
        hint.setPadding(0, px(6), 0, 0);
        perm.addView(hint);
        permBtn = button(tr("Mở cài đặt truy cập thông báo", "Open notification access settings"), true);
        permBtn.setOnClickListener(v -> openListenerSettings());
        perm.addView(permBtn, matchWrap(px(12)));
        Button battery = button(tr("Không tối ưu pin (chạy ngầm ổn định)", "Disable battery optimization"), false);
        battery.setOnClickListener(v -> askBattery());
        perm.addView(battery, matchWrap(px(8)));
        root.addView(perm);

        // --- bước 2: mã kênh
        LinearLayout code = card();
        code.addView(caption(tr("BƯỚC 2 · MÃ (NHẬP VÀO MÁY TÍNH)", "STEP 2 · CODE (ENTER IT ON YOUR PC)")));
        codeText = label("", 24, TEXT, true);
        codeText.setTypeface(Typeface.MONOSPACE, Typeface.BOLD);
        codeText.setGravity(Gravity.CENTER);
        codeText.setPadding(0, px(6), 0, px(6));
        code.addView(codeText, matchWrap(0));
        TextView ch = label(tr("Mã riêng để máy tính nhận bài từ điện thoại. Chỉ nhập vào máy của bạn, đừng đăng công khai.", "Your private code that lets your PC receive tracks from this phone. Only enter it on your own PC — don't post it publicly."), 12, MUTED, false);
        code.addView(ch);
        LinearLayout row = new LinearLayout(this);
        row.setOrientation(LinearLayout.HORIZONTAL);
        Button copy = button(tr("Sao chép", "Copy"), true);
        copy.setOnClickListener(v -> copyCode());
        Button share = button(tr("Gửi sang máy tính", "Send to PC"), false);
        share.setOnClickListener(v -> shareCode());
        row.addView(copy, weight(0));
        row.addView(share, weight(px(8)));
        code.addView(row, matchWrap(px(12)));
        Button adv = button(tr("Đổi mã / đổi server…", "Change code / server…"), false);
        adv.setOnClickListener(v -> advanced());
        code.addView(adv, matchWrap(px(8)));
        root.addView(code);

        // --- bước 3: tuỳ chọn
        LinearLayout opt = card();
        opt.addView(caption(tr("BƯỚC 3 · CHIA SẺ", "STEP 3 · SHARING")));
        opt.addView(toggle(tr("Đang chia sẻ", "Sharing"), "enabled", Prefs.enabled(this)));
        opt.addView(toggle("YouTube Music", "ytmusic", Prefs.ytmusic(this)));
        opt.addView(toggle("YouTube", "youtube", Prefs.youtube(this)));
        TextView qs = label(tr("Mẹo: kéo thanh thông báo › sửa ô nhanh › thêm ô \"YT Status\" để ẩn/hiện bằng một chạm.", "Tip: pull down the notification shade › edit quick settings › add the \"YT Status\" tile to hide/show with one tap."), 12, MUTED, false);
        qs.setPadding(0, px(8), 0, 0);
        opt.addView(qs);
        root.addView(opt);
    }

    @Override protected void onResume() {
        super.onResume();
        if (listenerAllowed() && !NowListener.connected && Build.VERSION.SDK_INT >= 24) {
            try { NotificationListenerService.requestRebind(new ComponentName(this, NowListener.class)); } catch (Exception ignored) {}
        }
        h.post(tick);
    }

    @Override protected void onPause() {
        h.removeCallbacks(tick);
        super.onPause();
    }

    private void refresh() {
        boolean ok = listenerAllowed();
        permState.setText(ok ? (NowListener.connected ? tr("● Đã cấp quyền, đang chạy ngầm", "● Granted, running in background")
                : tr("● Đã cấp quyền, đang khởi động…", "● Granted, starting…"))
                : tr("● Chưa cấp quyền truy cập thông báo", "● Notification access not granted"));
        permState.setTextColor(ok ? GREEN : RED);
        permBtn.setVisibility(ok ? View.GONE : View.VISIBLE);
        codeText.setText(Prefs.channel(this));

        String t = Sender.lastTrack;
        if (!Prefs.enabled(this)) t = tr("Đang tắt chia sẻ", "Sharing is off");
        nowText.setText(t == null || t.isEmpty() ? tr("Không phát gì", "Nothing playing") : t);
        statusText.setText(Sender.lastStatus == null ? tr("Chưa gửi gì.", "Nothing sent yet.") : Sender.lastStatus);
    }

    private void copyCode() {
        ClipboardManager cm = (ClipboardManager) getSystemService(CLIPBOARD_SERVICE);
        cm.setPrimaryClip(ClipData.newPlainText("YT Status", Prefs.channel(this)));
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
        TextView a = label(tr("Server (để trống = ntfy.sh). Chỉ đổi nếu bạn tự dựng server ntfy riêng.", "Server (empty = ntfy.sh). Only change it if you run your own ntfy server."), 13, MUTED, false);
        box.addView(a);
        EditText srv = input(Prefs.DEFAULT_SERVER);
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
                    Prefs.newChannel(this);
                    refresh();
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

    private TextView label(String s, int sp, int color, boolean bold) {
        TextView t = new TextView(this);
        t.setText(s);
        t.setTextSize(sp);
        t.setTextColor(color);
        if (bold) t.setTypeface(Typeface.DEFAULT_BOLD);
        t.setLineSpacing(0, 1.15f);
        return t;
    }

    private TextView caption(String s) {
        TextView t = label(s, 11, MUTED, true);
        t.setLetterSpacing(0.08f);
        t.setPadding(0, 0, 0, px(8));
        return t;
    }

    private LinearLayout card() {
        LinearLayout c = new LinearLayout(this);
        c.setOrientation(LinearLayout.VERTICAL);
        c.setPadding(px(16), px(16), px(16), px(16));
        GradientDrawable g = new GradientDrawable();
        g.setColor(CARD);
        g.setCornerRadius(px(16));
        g.setStroke(px(1), LINE);
        c.setBackground(g);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.WRAP_CONTENT);
        lp.bottomMargin = px(12);
        c.setLayoutParams(lp);
        return c;
    }

    private Button button(String s, boolean primary) {
        Button b = new Button(this);
        b.setText(s);
        b.setAllCaps(false);
        b.setTextSize(15);
        b.setTextColor(primary ? Color.WHITE : TEXT);
        b.setGravity(Gravity.CENTER);       // chữ căn giữa tuyệt đối
        b.setIncludeFontPadding(false);
        b.setMinHeight(px(48));
        b.setMinimumHeight(px(48));
        b.setPadding(px(12), 0, px(12), 0);
        b.setStateListAnimator(null);
        GradientDrawable g = new GradientDrawable();
        g.setColor(primary ? RED : 0xFF262930);
        g.setCornerRadius(px(12));
        b.setBackground(g);
        return b;
    }

    private EditText input(String hint) {
        EditText e = new EditText(this);
        e.setHint(hint);
        e.setSingleLine(true);
        e.setInputType(InputType.TYPE_TEXT_VARIATION_URI);
        return e;
    }

    private Switch toggle(String text, String key, boolean value) {
        Switch s = new Switch(this);
        s.setText(text);
        s.setTextColor(TEXT);
        s.setTextSize(15);
        s.setChecked(value);
        s.setMinHeight(px(44));
        // theme tối mặc định vẽ công tắc đen trên nền đen -> tô màu rõ ràng: bật = đỏ, tắt = xám
        int[][] states = { { android.R.attr.state_checked }, {} };
        s.setThumbTintList(new ColorStateList(states, new int[] { Color.WHITE, 0xFFB8BCC4 }));
        s.setTrackTintList(new ColorStateList(states, new int[] { RED, 0xFF4A4E57 }));
        s.setOnCheckedChangeListener((CompoundButton b, boolean on) -> {
            Prefs.put(this, key, on);
            if ("enabled".equals(key)) ToggleTile.refresh(this);
        });
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
