# YT Status

[Tiếng Việt](README.md) · **English**

Shows what you're listening to / watching on **YouTube** and **YouTube Music** as your Discord status, just like the Spotify card:

```
LISTENING TO YOUTUBE MUSIC              WATCHING YOUTUBE
[cover]  Song title                     [thumb]  Video title
         Artist                                  Channel
         ━━━━━━━○──── 1:23 / 4:08                ━━━━○────── 3:10 / 12:45
```

It picks up tracks from two places:

- **Your PC's browser** (Chrome, Edge, Brave, Opera…): only `YTStatus.exe` is needed.
- **Your Android phone** (YouTube, YouTube Music apps): also install `YTStatus.apk`.

Discord only lets this kind of status be set through the Discord app on a computer, so it shows while your PC is on with Discord open (the desktop app, not the web version).

Download `YTStatus.exe` and `YTStatus.apk` from **Releases** on the right.

Both apps have a **Tiếng Việt / English** switch at the top. The first launch follows your Windows / phone language.

---

## 1. Create a Discord app (once, 2 minutes)

The status shows the **app's name** ("Listening to **YouTube Music**"), so everyone creates their own app with that name:

1. Go to https://discord.com/developers/applications → **New Application** → name it `YouTube Music` → Create.
2. Copy the **Application ID** on the General Information page.
3. Optional: create a second app named `YouTube` so videos show "Watching **YouTube**".
4. Optional — small icon on the cover: in the app › **Rich Presence › Art Assets** → add [`pc/anh_cho_discord/ytmusic.png`](pc/anh_cho_discord/ytmusic.png) named `ytmusic` (for the YouTube app: `youtube.png` named `youtube`).

No bot and no token needed.

## 2. YTStatus.exe (PC)

1. Download `YTStatus.exe`, put it anywhere (no install), double-click to run.
   If Windows says "Windows protected your PC", click **More info › Run anyway** (the file isn't code-signed).
2. Paste your **Client ID** into the "YouTube Music" box (and the "YouTube" box if you made one), click **Save**.
3. Tick **Start with Windows** if you want it to start automatically.
4. Click **Test Discord**: a test track shows on your status for 20 seconds.

Play something on YouTube in your browser and your Discord status follows.

| Action | Result |
|---|---|
| Click **X** | Hides to the system tray, keeps running |
| Click the tray icon | Opens the window again |
| Right-click the tray icon › **Exit** | Quits completely |
| Tick **Don't show on Discord** | Keeps running but shows nothing |
| Untick **Share YouTube Music** / **Share YouTube** | Stops showing that one for now (also in the tray right-click menu) |
| **Tiếng Việt / English** box (top right) | Switches language instantly |

Settings are saved in `YTStatus.json` next to the exe. To remove it: untick "Start with Windows", Exit, delete the files.

## 3. YTStatus.apk (Android phone — optional)

1. Install `YTStatus.apk`. **Google Play Protect may block it** in some countries (including Vietnam) because the app needs notification access (to see what's playing). To install:
   - Open **Play Store** › your avatar › **Play Protect** › ⚙ › turn off **Scan apps with Play Protect** → install the APK → **turn it back on** right away.
2. Open the app → enable **Notification access** for YT Status.
   Switch greyed out (Android 13+): Settings › Apps › YT Status › **⋮** › **Allow restricted settings**, then enable it.
3. Tap **Disable battery optimization** (on Xiaomi / Oppo / Samsung also allow "Autostart").
4. The **English / Tiếng Việt** button at the top switches the app's language.
5. Copy the **Code** shown in the app (like `nc-xxxx-xxxx-xxxx`) into the **Phone code** box in YTStatus.exe, click Save.

The phone sends tracks through [ntfy.sh](https://ntfy.sh) (free, 250 messages/day — the app only sends on track change / pause / seek, enough for a full day). Don't share your code publicly.

If something is playing on both the PC and the phone, the PC wins.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Nothing shows on Discord | Check the status lines in the YTStatus window. Make sure "Don't show on Discord" is unticked, you're using the Discord **desktop app**, and Discord › Settings › Activity Privacy › "Share your detected activities" is on |
| "Computer" line says "not a YouTube tab" | Music is playing in another app (Spotify…) — only YouTube is picked up |
| Phone stops sending after the screen is locked for a while | Disable battery optimization + allow autostart for the app |
| Anything else | Send `YTStatus.log` (next to the exe) to whoever shared the app |

---

## Building from source

- `BUILD_EXE.bat`: builds YTStatus.exe with the compiler built into Windows (a few seconds).
- `BUILD_APK.bat`: builds YTStatus.apk (first run downloads a JDK + Android tools, ~600 MB). Creates a new signing key in `android/khoa_ky/` if there isn't one.
- `CAI_QUA_USB.bat`: installs the APK over USB (not blocked by Play Protect).
