package app.ytstatus;

import android.content.ComponentName;
import android.content.Context;
import android.content.SharedPreferences;
import android.graphics.Bitmap;
import android.media.MediaMetadata;
import android.media.session.MediaController;
import android.media.session.MediaSessionManager;
import android.media.session.PlaybackState;
import android.os.Handler;
import android.os.Looper;
import android.os.SystemClock;
import android.service.notification.NotificationListenerService;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;

/**
 * Hệ thống tự giữ dịch vụ này chạy ngầm khi app được cấp quyền "Truy cập thông báo".
 * Nó KHÔNG đọc nội dung thông báo — chỉ xem phiên phát nhạc (MediaSession) của
 * YouTube / YouTube Music: tên bài, kênh, ảnh bìa, vị trí đang phát.
 *
 * Tiết kiệm lượt gửi (ntfy.sh miễn phí cho 250 tin/ngày): chỉ gửi khi đổi bài, tạm dừng/phát tiếp
 * hoặc tua; bài có thời lượng thì PC tự biết lúc hết, không cần nhắc lại.
 */
public class NowListener extends NotificationListenerService
        implements MediaSessionManager.OnActiveSessionsChangedListener,
        SharedPreferences.OnSharedPreferenceChangeListener {

    private static final long DEBOUNCE_MS = 1500;
    private static final long LIVE_HEARTBEAT_MS = 10 * 60 * 1000;   // chỉ cho livestream (không rõ thời lượng)
    private static final long ART_KEEP_MS = 150 * 60 * 1000;        // ntfy giữ ảnh 3 giờ, dùng lại tối đa 2,5 giờ

    static volatile boolean connected = false;
    private static NowListener instance;

    private final Handler h = new Handler(Looper.getMainLooper());
    private final Map<MediaController.Callback, MediaController> watched = new HashMap<>();
    private MediaSessionManager msm;
    private String lastSig = null;
    private long lastStart = 0;
    private boolean shown = false;
    // ảnh bìa đã tải lên cho bài hiện tại
    private String artFor = null, artUrl = null;
    private long artAt = 0;

    private GameWatcher games;
    private final Runnable flush = this::evaluate;
    private final Runnable heartbeat = () -> { lastSig = null; evaluate(); };

    // ------------------------------------------------------------- vòng đời
    @Override public void onListenerConnected() {
        super.onListenerConnected();
        connected = true;
        instance = this;
        msm = (MediaSessionManager) getSystemService(Context.MEDIA_SESSION_SERVICE);
        ComponentName me = new ComponentName(this, NowListener.class);
        try {
            msm.addOnActiveSessionsChangedListener(this, me, h);
            onActiveSessionsChanged(msm.getActiveSessions(me));
        } catch (SecurityException e) {
            Sender.lastStatus = Prefs.t(this, "Chưa có quyền truy cập thông báo.", "Notification access not granted.");
        }
        Prefs.sp(this).registerOnSharedPreferenceChangeListener(this);
        games = new GameWatcher(this);
        games.start();
    }

    @Override public void onListenerDisconnected() {
        connected = false;
        if (instance == this) instance = null;
        try { if (msm != null) msm.removeOnActiveSessionsChangedListener(this); } catch (Exception ignored) {}
        Prefs.sp(this).unregisterOnSharedPreferenceChangeListener(this);
        if (games != null) games.stop();
        unwatchAll();
        h.removeCallbacksAndMessages(null);
        super.onListenerDisconnected();
    }

    /** Màn hình chính gọi (nút "Gửi lại", đổi mã kênh). */
    static boolean forceSend() {
        NowListener s = instance;
        if (s == null) return false;
        s.h.post(() -> { s.lastSig = null; s.artFor = null; s.evaluate(); });
        if (s.games != null) s.games.resend();
        return true;
    }

    @Override public void onSharedPreferenceChanged(SharedPreferences sp, String key) {
        if ("enabled".equals(key) || "youtube".equals(key) || "ytmusic".equals(key)) {
            lastSig = null;
            schedule();
        }
        if (games != null && ("enabled".equals(key) || "games".equals(key) || "game_off".equals(key) || "game_extra".equals(key))) games.poke();
    }

    // ------------------------------------------------------------- theo dõi phiên
    @Override public void onActiveSessionsChanged(List<MediaController> list) {
        unwatchAll();
        if (list != null) {
            for (MediaController mc : list) {
                if (Prefs.appOf(mc.getPackageName()) == null) continue;   // chỉ YouTube / YT Music
                final MediaController ctl = mc;
                MediaController.Callback cb = new MediaController.Callback() {
                    @Override public void onMetadataChanged(MediaMetadata m) { schedule(); }
                    @Override public void onPlaybackStateChanged(PlaybackState s) { schedule(); }
                    @Override public void onSessionDestroyed() {
                        try { ctl.unregisterCallback(this); } catch (Exception ignored) {}
                        watched.remove(this);
                        schedule();
                    }
                };
                ctl.registerCallback(cb, h);
                watched.put(cb, ctl);
            }
        }
        schedule();
    }

    private void unwatchAll() {
        for (Map.Entry<MediaController.Callback, MediaController> e : watched.entrySet()) {
            try { e.getValue().unregisterCallback(e.getKey()); } catch (Exception ignored) {}
        }
        watched.clear();
    }

    private void schedule() {
        h.removeCallbacks(flush);
        h.postDelayed(flush, DEBOUNCE_MS);
    }

    // ------------------------------------------------------------- quyết định gửi
    private void evaluate() {
        h.removeCallbacks(heartbeat);
        MediaController best = Prefs.enabled(this) ? pickBest() : null;
        Sender.Snap snap = best == null ? null : snapshot(best);

        if (snap == null) {
            if (shown) Sender.sendStop(this);
            shown = false;
            lastSig = "STOP";
            Sender.lastTrack = "";
            return;
        }

        Bitmap art = artOf(best.getMetadata());
        String trackKey = snap.app + "|" + snap.title + "|" + snap.artist;
        long start = System.currentTimeMillis() - snap.positionMs;
        String sig = trackKey + "|" + snap.album + "|" + snap.playing + "|" + snap.durationMs + "|" + (art != null);
        boolean seeked = snap.playing && Math.abs(start - lastStart) > 4000;

        if (!sig.equals(lastSig) || seeked) {
            boolean haveArt = trackKey.equals(artFor) && artUrl != null
                    && SystemClock.elapsedRealtime() - artAt < ART_KEEP_MS;
            final String key = trackKey;
            Sender.sendTrack(this, snap, haveArt ? null : art, haveArt ? artUrl : null, (ok, url) -> {
                if (ok && url != null) h.post(() -> {
                    if (!key.equals(artFor) || artUrl == null) {
                        artFor = key;
                        artUrl = url;
                        artAt = SystemClock.elapsedRealtime();
                    }
                });
            });
            lastSig = sig;
            lastStart = start;
            shown = true;
        }
        if (snap.playing && snap.durationMs <= 0) h.postDelayed(heartbeat, LIVE_HEARTBEAT_MS);
    }

    /** Ưu tiên phiên đang phát; không có thì phiên tạm dừng. */
    private MediaController pickBest() {
        MediaController playing = null, paused = null;
        for (MediaController mc : new ArrayList<>(watched.values())) {
            if (!Prefs.wanted(this, mc.getPackageName())) continue;
            MediaMetadata m = mc.getMetadata();
            if (m == null || text(m, MediaMetadata.METADATA_KEY_TITLE).isEmpty()) continue;
            PlaybackState st = mc.getPlaybackState();
            int s = st == null ? PlaybackState.STATE_NONE : st.getState();
            if (s == PlaybackState.STATE_PLAYING || s == PlaybackState.STATE_BUFFERING) {
                if (playing == null) playing = mc;
            } else if (s == PlaybackState.STATE_PAUSED) {
                if (paused == null) paused = mc;
            }
        }
        return playing != null ? playing : paused;
    }

    private Sender.Snap snapshot(MediaController mc) {
        MediaMetadata m = mc.getMetadata();
        PlaybackState st = mc.getPlaybackState();
        if (m == null) return null;
        Sender.Snap s = new Sender.Snap();
        s.app = Prefs.appOf(mc.getPackageName());
        s.title = text(m, MediaMetadata.METADATA_KEY_TITLE);
        if (s.title.isEmpty()) s.title = text(m, MediaMetadata.METADATA_KEY_DISPLAY_TITLE);
        s.artist = text(m, MediaMetadata.METADATA_KEY_ARTIST);
        if (s.artist.isEmpty()) s.artist = text(m, MediaMetadata.METADATA_KEY_ALBUM_ARTIST);
        if (s.artist.isEmpty()) s.artist = text(m, MediaMetadata.METADATA_KEY_DISPLAY_SUBTITLE);
        s.album = text(m, MediaMetadata.METADATA_KEY_ALBUM);
        s.mediaId = text(m, MediaMetadata.METADATA_KEY_MEDIA_ID);
        if (!s.mediaId.matches("[A-Za-z0-9_-]{11}")) s.mediaId = "";
        s.durationMs = Math.max(0, m.getLong(MediaMetadata.METADATA_KEY_DURATION));
        if (st != null) {
            int state = st.getState();
            s.playing = state == PlaybackState.STATE_PLAYING || state == PlaybackState.STATE_BUFFERING;
            long pos = st.getPosition();
            if (s.playing && st.getLastPositionUpdateTime() > 0) {
                float speed = st.getPlaybackSpeed() <= 0 ? 1f : st.getPlaybackSpeed();
                pos += (long) ((SystemClock.elapsedRealtime() - st.getLastPositionUpdateTime()) * speed);
            }
            if (s.durationMs > 0) pos = Math.min(pos, s.durationMs);
            s.positionMs = Math.max(0, pos);
        }
        return s;
    }

    private static Bitmap artOf(MediaMetadata m) {
        if (m == null) return null;
        Bitmap art = m.getBitmap(MediaMetadata.METADATA_KEY_ALBUM_ART);
        if (art == null) art = m.getBitmap(MediaMetadata.METADATA_KEY_ART);
        if (art == null) art = m.getBitmap(MediaMetadata.METADATA_KEY_DISPLAY_ICON);
        return art;
    }

    private static String text(MediaMetadata m, String key) {
        CharSequence cs = m.getText(key);
        return cs == null ? "" : cs.toString().trim();
    }
}
