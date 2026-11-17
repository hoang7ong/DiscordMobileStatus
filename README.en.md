# Discord Mobile Status

[Tiếng Việt](README.md) · **English**

*(Formerly: YT Status)*

Shows what you're **listening to / watching / playing** as your Discord status, like the Spotify card:

```
LISTENING TO YOUTUBE MUSIC       PLAYING
[cover]  Song title              [icon]  Arena of Valor
         Artist                          On mobile
         ━━━━━○──── 1:23 / 4:08          ⏱ 23:14 elapsed
```

Sources:

- **YouTube / YouTube Music in your PC's browser** (Chrome, Edge, Brave, Opera…): only `DiscordMobileStatus.exe` is needed.
- **YouTube / YouTube Music on your Android phone**: also install `DiscordMobileStatus.apk`.
- **Games on your Android phone** (Arena of Valor, Wild Rift, PUBG Mobile, Free Fire…): shows the game name, icon and elapsed time. The app does **not** read your hero, rank, score or any in-game data.

Music and a game can show **at the same time**.

Discord only lets this kind of status be set through the Discord app on a computer, so it shows while your PC is on with Discord open (the desktop app, not the web version).

Download `DiscordMobileStatus.exe` and `DiscordMobileStatus.apk` from **Releases** on the right.

Both apps have a **Tiếng Việt / English** switch. The first launch follows your Windows / phone language.

---

## 1. DiscordMobileStatus.exe (PC)

No need to create Discord apps or turn on Developer Mode — the "YouTube Music", "YouTube" and "Phone games" apps are built in.

1. Download `DiscordMobileStatus.exe`, put it anywhere (no install), double-click to run.
   If Windows says "Windows protected your PC", click **More info › Run anyway** (the file isn't code-signed).
2. Open Discord on your PC and play something on YouTube in your browser — done.
3. Click **Test Discord** to preview: a test track shows on your status for 20 seconds.

The window is split into blocks, every option is a switch:

| Block | Switches |
|---|---|
| **Sharing** | **Share on Discord** — off hides everything and folds the sub-switches away. On reveals: YouTube Music, YouTube, Phone games, Show while paused |
| **Sources** | **This PC's browser**; **Android phone** — on reveals the phone-code box |
| **App** | **Start with Windows** (starts hidden in the tray) |

| Action | Result |
|---|---|
| Click **X** | Hides to the system tray, keeps running |
| Click the tray icon | Opens the window again |
| Right-click the tray icon | Quick toggles per type, **Exit** to quit |
| **Tiếng Việt / English** switch (top right) | Switches language instantly |

Settings are saved in `DiscordMobileStatus.json` next to the exe. Settings from older versions move over automatically.

## 2. DiscordMobileStatus.apk (Android phone — optional)

1. Install `DiscordMobileStatus.apk` (installs over the old YT Status, your code stays the same). **Google Play Protect may block it** in some countries because the app needs notification access. To install:
   - Open **Play Store** › your avatar › **Play Protect** › ⚙ › turn off **Scan apps with Play Protect** → install the APK → **turn it back on** right away.
2. Open the app → enable **Notification access** (to see what's playing).
   Switch greyed out (Android 13+): Settings › Apps › Discord Mobile Status › **⋮** › **Allow restricted settings**, then enable it.
3. To share games: tap **Open usage access settings** → allow Discord Mobile Status. This is the same permission Digital Wellbeing uses — it only knows which app is open.
4. Tap **Disable battery optimization** (on Xiaomi / Oppo / Samsung also allow "Autostart").
5. **Sharing** block › turn on **Games** › **Choose which games to share…**: detected games are on by default; tick other apps if you like, untick games you don't want anyone to see. Non-game apps are never shared unless you tick them.
6. On the PC, **Sources** block › turn on **Android phone**, paste the **Code** shown in the app (like `nc-xxxx-xxxx-xxxx`), click Save.

The phone sends through [ntfy.sh](https://ntfy.sh) (free, 250 messages/day). It only sends on track change / pause / seek / game start or exit — enough for a full day. Don't share your code publicly.

Battery: games are checked every 15 seconds **only while the screen is on**; with the screen off it stops completely. Leaving the game for more than 30 seconds (or turning the screen off) hides the game status.

If music is playing on both the PC and the phone, the PC wins.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Nothing shows on Discord | Check the status lines in the window. Make sure "Share on Discord" is on, you use the Discord **desktop app**, and Discord › Settings › Activity Privacy › "Share your detected activities" is on |
| Music shows but the game doesn't (or the other way round) | Report it to whoever shared the app, with `DiscordMobileStatus.log` |
| "Computer" line says "not a YouTube tab" | Music is playing in another app (Spotify…) — only YouTube is picked up |
| Phone stops sending after the screen is locked for a while | Disable battery optimization + allow autostart for the app |
| Anything else | Send `DiscordMobileStatus.log` (next to the exe) to whoever shared the app |

---

## Building from source

- `BUILD_EXE.bat`: builds DiscordMobileStatus.exe with the compiler built into Windows (a few seconds).
- `BUILD_APK.bat`: builds DiscordMobileStatus.apk (first run downloads a JDK + Android tools, ~600 MB).
- `CAI_QUA_USB.bat`: installs the APK over USB (not blocked by Play Protect).
