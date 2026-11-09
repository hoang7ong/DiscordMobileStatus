package app.ytstatus;

import android.content.ComponentName;
import android.content.Context;
import android.service.quicksettings.Tile;
import android.service.quicksettings.TileService;

/** Ô bật/tắt trong thanh cài đặt nhanh: một chạm để ẩn bài đang nghe khỏi Discord. */
public class ToggleTile extends TileService {

    static void refresh(Context c) {
        try { requestListeningState(c, new ComponentName(c, ToggleTile.class)); } catch (Exception ignored) {}
    }

    @Override public void onStartListening() { paint(); }

    @Override public void onClick() {
        Prefs.put(this, "enabled", !Prefs.enabled(this));   // NowListener nghe thay đổi này và tự gửi / ẩn
        paint();
    }

    private void paint() {
        Tile t = getQsTile();
        if (t == null) return;
        boolean on = Prefs.enabled(this);
        t.setState(on ? Tile.STATE_ACTIVE : Tile.STATE_INACTIVE);
        t.setLabel("YT Status");
        if (android.os.Build.VERSION.SDK_INT >= 29) t.setSubtitle(on ? Prefs.t(this, "Đang chia sẻ", "Sharing") : Prefs.t(this, "Đã ẩn", "Hidden"));
        t.updateTile();
    }
}
