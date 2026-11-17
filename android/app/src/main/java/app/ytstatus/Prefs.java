package app.ytstatus;

import android.content.Context;
import android.content.SharedPreferences;

import java.security.SecureRandom;

/** Cài đặt lưu trên máy. */
final class Prefs {
    static final String DEFAULT_SERVER = "https://ntfy.sh";
    private static final String ALPHABET = "abcdefghjkmnpqrstuvwxyz23456789";   // bỏ i, l, o, 0, 1 cho dễ đọc

    private Prefs() {}

    private static volatile SharedPreferences cached;

    static SharedPreferences sp(Context c) {
        SharedPreferences s = cached;
        if (s == null) cached = s = c.getApplicationContext().getSharedPreferences("ytstatus", Context.MODE_PRIVATE);
        return s;
    }

    /** Mã kênh, tự tạo lần đầu. Ai biết mã này thì thấy bài bạn nghe — đừng đăng công khai. */
    static String channel(Context c) {
        String t = sp(c).getString("channel", "");
        if (t.isEmpty()) t = newChannel(c);
        return t;
    }

    static String newChannel(Context c) {
        SecureRandom r = new SecureRandom();
        StringBuilder sb = new StringBuilder("nc");
        for (int i = 0; i < 12; i++) {
            if (i % 4 == 0) sb.append('-');
            sb.append(ALPHABET.charAt(r.nextInt(ALPHABET.length())));
        }
        String t = sb.toString();
        sp(c).edit().putString("channel", t).apply();
        return t;
    }

    static String server(Context c) {
        String s = sp(c).getString("server", DEFAULT_SERVER).trim();
        while (s.endsWith("/")) s = s.substring(0, s.length() - 1);
        if (s.isEmpty()) return DEFAULT_SERVER;
        if (!s.startsWith("http://") && !s.startsWith("https://")) s = "https://" + s;
        return s;
    }

    static boolean enabled(Context c) { return sp(c).getBoolean("enabled", true); }

    /** Ngôn ngữ: "vi" / "en". Lần đầu theo ngôn ngữ của máy. */
    static volatile Boolean en;

    static boolean en(Context c) {
        if (en == null) {
            String l = sp(c).getString("lang", "");
            if (l.isEmpty()) l = "vi".equals(java.util.Locale.getDefault().getLanguage()) ? "vi" : "en";
            en = "en".equals(l);
        }
        return en;
    }

    static void setEn(Context c, boolean v) {
        en = v;
        sp(c).edit().putString("lang", v ? "en" : "vi").apply();
    }

    /** Chữ theo ngôn ngữ đang chọn. */
    static String t(Context c, String vi, String english) { return en(c) ? english : vi; }
    static boolean youtube(Context c) { return sp(c).getBoolean("youtube", true); }
    static boolean games(Context c) { return sp(c).getBoolean("games", true); }
    static boolean ytmusic(Context c) { return sp(c).getBoolean("ytmusic", true); }

    static void put(Context c, String key, String v) { sp(c).edit().putString(key, v).apply(); }
    static void put(Context c, String key, boolean v) { sp(c).edit().putBoolean(key, v).apply(); }

    /** Gói này là YouTube / YouTube Music (kể cả bản ReVanced)? Trả "youtube", "ytmusic" hoặc null. */
    static String appOf(String pkg) {
        if (pkg == null) return null;
        if (pkg.equals("com.google.android.apps.youtube.music") || pkg.contains("youtube.music")
                || pkg.endsWith(".music.revanced")) return "ytmusic";
        if (pkg.equals("com.google.android.youtube") || pkg.contains("revanced.android.youtube")
                || pkg.contains("rvx.android.youtube") || pkg.equals("com.vanced.android.youtube")) return "youtube";
        return null;
    }

    static boolean wanted(Context c, String pkg) {
        String a = appOf(pkg);
        if (a == null) return false;
        return a.equals("ytmusic") ? ytmusic(c) : youtube(c);
    }
}
