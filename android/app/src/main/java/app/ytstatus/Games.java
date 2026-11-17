package app.ytstatus;

import android.app.AppOpsManager;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.os.Build;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** App nào là game, game nào được chia sẻ. */
final class Games {
    private Games() {}

    /** Game hay gặp ở Việt Nam mà có bản không tự khai báo là "game" với Android. */
    private static final String[] KNOWN = {
            "com.garena.game.kgvn", "com.garena.game.kgth", "com.garena.game.kgtw", "com.ngame.allstar.eu",   // Liên Quân Mobile / AoV
            "com.riotgames.league.wildrift", "com.riotgames.league.wildriftvn",                                 // Liên Minh Tốc Chiến
            "com.riotgames.league.teamfighttactics", "com.riotgames.league.teamfighttacticsvn",                // ĐTCL / TFT
            "com.vng.pubgmobile", "com.tencent.ig", "com.pubg.imobile",                                        // PUBG Mobile
            "com.dts.freefireth", "com.dts.freefiremax",                                                       // Free Fire
            "com.mobile.legends", "com.vng.mlbbvn",                                                            // Mobile Legends
            "com.vng.codmvn", "com.activision.callofduty.shooter",                                             // Call of Duty Mobile
            "com.miHoYo.GenshinImpact", "com.HoYoverse.hkrpgoversea", "com.HoYoverse.Nap",                     // HoYoverse
            "com.roblox.client", "com.mojang.minecraftpe", "com.supercell.clashofclans", "com.supercell.clashroyale",
            "com.supercell.brawlstars", "com.kiloo.subwaysurf", "com.zingplay.playtogether", "com.haegin.playtogether",
            "com.vng.playtogether", "com.netease.newspike", "com.levelinfinite.sgameGlobal", "com.tencent.tmgp.sgame",
    };
    private static final Set<String> KNOWN_SET = new HashSet<>();
    static { Collections.addAll(KNOWN_SET, KNOWN); }

    private static final Map<String, Boolean> gameCache = new HashMap<>();

    /** Android có xếp app này vào loại game không (hoặc có trong danh sách quen). Nhớ kết quả để khỏi hỏi lại. */
    static boolean isGame(Context c, String pkg) {
        if (pkg == null) return false;
        synchronized (gameCache) {
            Boolean v = gameCache.get(pkg);
            if (v != null) return v;
        }
        boolean g = KNOWN_SET.contains(pkg);
        if (!g) {
            try {
                ApplicationInfo ai = c.getPackageManager().getApplicationInfo(pkg, 0);
                if (Build.VERSION.SDK_INT >= 26 && ai.category == ApplicationInfo.CATEGORY_GAME) g = true;
                else if ((ai.flags & ApplicationInfo.FLAG_IS_GAME) != 0) g = true;
            } catch (Exception ignored) {}
        }
        synchronized (gameCache) { gameCache.put(pkg, g); }
        return g;
    }

    static Set<String> off(Context c) { return new HashSet<>(Prefs.sp(c).getStringSet("game_off", new HashSet<String>())); }
    static Set<String> extra(Context c) { return new HashSet<>(Prefs.sp(c).getStringSet("game_extra", new HashSet<String>())); }

    /** App này có được chia sẻ lên Discord không. */
    static boolean shared(Context c, String pkg) {
        if (pkg == null || pkg.equals(c.getPackageName()) || Prefs.appOf(pkg) != null) return false;
        if (extra(c).contains(pkg)) return true;
        return isGame(c, pkg) && !off(c).contains(pkg);
    }

    static String label(Context c, String pkg) {
        try {
            PackageManager pm = c.getPackageManager();
            return pm.getApplicationLabel(pm.getApplicationInfo(pkg, 0)).toString();
        } catch (Exception e) {
            return pkg;
        }
    }

    static final class App {
        String pkg, label;
        boolean game;
    }

    /** Mọi app có biểu tượng ngoài màn hình chính; game xếp trước. */
    static List<App> launcherApps(Context c) {
        PackageManager pm = c.getPackageManager();
        Intent i = new Intent(Intent.ACTION_MAIN);
        i.addCategory(Intent.CATEGORY_LAUNCHER);
        List<ResolveInfo> list = pm.queryIntentActivities(i, 0);
        Map<String, App> map = new HashMap<>();
        for (ResolveInfo ri : list) {
            String pkg = ri.activityInfo.packageName;
            if (map.containsKey(pkg) || pkg.equals(c.getPackageName()) || Prefs.appOf(pkg) != null) continue;
            App a = new App();
            a.pkg = pkg;
            a.label = ri.loadLabel(pm).toString();
            a.game = isGame(c, pkg);
            map.put(pkg, a);
        }
        List<App> out = new ArrayList<>(map.values());
        Collections.sort(out, (x, y) -> {
            if (x.game != y.game) return x.game ? -1 : 1;
            return x.label.compareToIgnoreCase(y.label);
        });
        return out;
    }

    private static volatile boolean uaCache;
    private static volatile long uaAt;

    /** Đã cấp quyền "Truy cập dữ liệu sử dụng" chưa (nhớ 30 giây; màn hình chính gọi fresh=true). */
    static boolean hasUsageAccess(Context c) { return hasUsageAccess(c, false); }

    static boolean hasUsageAccess(Context c, boolean fresh) {
        long now = android.os.SystemClock.elapsedRealtime();
        if (!fresh && uaAt != 0 && now - uaAt < 30000) return uaCache;
        uaCache = checkUsage(c);
        uaAt = now;
        return uaCache;
    }

    private static boolean checkUsage(Context c) {
        try {
            AppOpsManager ops = (AppOpsManager) c.getSystemService(Context.APP_OPS_SERVICE);
            int mode = ops.checkOpNoThrow(AppOpsManager.OPSTR_GET_USAGE_STATS, android.os.Process.myUid(), c.getPackageName());
            if (mode == AppOpsManager.MODE_DEFAULT) {
                return c.checkCallingOrSelfPermission("android.permission.PACKAGE_USAGE_STATS") == PackageManager.PERMISSION_GRANTED;
            }
            return mode == AppOpsManager.MODE_ALLOWED;
        } catch (Exception e) {
            return false;
        }
    }
}
