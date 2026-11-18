# Discord Mobile Status

[Tiếng Việt](README.md) · **English**

Shows what you're **listening to, watching, scrolling or playing** as your Discord status — like the Spotify card, but for YouTube, SoundCloud, TikTok, Instagram and phone games.

```
LISTENING TO YOUTUBE MUSIC       PLAYING
[cover]  Song title              [icon]  Arena of Valor
         Artist                          On mobile
         ━━━━━○──── 1:23 / 4:08          ⏱ 23:14
```

## Features

| | PC | Phone | Default |
|---|:-:|:-:|:-:|
| YouTube Music | ✓ | ✓ | On |
| YouTube | ✓ | ✓ | On |
| SoundCloud | ✓ | ✓ | On |
| TikTok | ✓ | ✓ ¹ | Off |
| Instagram Reels | ✓ | ✓ ² | Off |
| Games (Arena of Valor, Wild Rift, PUBG, Free Fire…) | — | ✓ | On |

<sub>¹ On the phone it only knows you're on TikTok, not the video title. ² On the phone it shows whenever Instagram is open, DMs included.</sub>

- **Title, cover art and time** — just like Spotify.
- **Music and a game show at the same time.**
- **Works out of the box** — no Discord apps to create, no extra accounts.
- **Toggle each type** with switches; one switch hides everything. A quick-settings tile on the phone hides it with one tap.
- **Pick which games show** — untick the ones you'd rather keep private.
- **Lightweight**: lives in the tray with near-zero CPU; the phone only checks while the screen is on.
- **Private**: never reads messages, the screen or in-game data — it only knows which app is open. Never touches your Discord token.
- **Tiếng Việt / English.**

> **Spotify?** Discord has it built in: Settings › **Connections** › **Spotify**. You don't need this app.

## Setup

Download `DiscordMobileStatus.exe` (PC) and `DiscordMobileStatus.apk` (phone, optional) from **Releases**.

**PC**

1. Double-click `DiscordMobileStatus.exe` (no install). If Windows warns you, click **More info › Run anyway**.
2. Open the Discord **desktop app** and play something in your browser. Done.

Clicking **X** hides it to the tray. Right-click the tray icon for quick toggles or **Exit**. Turn on **Start with Windows** to have it start with your PC.

**Android phone** (to share what you listen to / play on the phone)

1. Install the APK. If Play Protect blocks it: Play Store › your avatar › Play Protect › ⚙ › temporarily turn off **Scan apps**, install, turn it back on.
2. Open the app and grant the 2 permissions using its buttons: **Notification access** (music / video) and **Usage access** (games, TikTok, Instagram). Also tap **Disable battery optimization**.
3. On the PC: **Sources** block › turn on **Android phone** › paste the **code** shown in the phone app › **Save**.

> The status only shows while Discord is open on your PC — that's a Discord limitation.

## Troubleshooting

| Problem | Fix |
|---|---|
| Nothing shows on Discord | Use the Discord **desktop app** (not the web version) and turn on Settings › Activity Privacy › **Share your detected activities**. Check the status lines in the app window. |
| TikTok / Instagram don't show | Both are **off by default** — turn them on in the Sharing block (PC and phone). |
| Permission switch greyed out (Android 13+) | Settings › Apps › Discord Mobile Status › **⋮** › **Allow restricted settings**. |
| Stops after the screen is locked a while | Disable battery optimization and allow **Autostart** for the app (Xiaomi, Oppo, Samsung). |
| Still shows after leaving a game | Wait about 30 seconds. |
| Anything else | Send `DiscordMobileStatus.log` (next to the exe) to whoever shared the app. |

---

<sub>The phone sends through [ntfy.sh](https://ntfy.sh) using a random private code — don't post it publicly. Build yourself: `BUILD_EXE.bat`, `BUILD_APK.bat`. Formerly: YT Status.</sub>
