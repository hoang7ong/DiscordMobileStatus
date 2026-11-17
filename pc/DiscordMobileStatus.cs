// Discord Mobile Status (tên cũ: YT Status) — hiện bạn đang nghe / xem / chơi gì lên trạng thái Discord:
// YouTube, YouTube Music (điện thoại + trình duyệt PC) và game trên điện thoại.
// Một file .exe, không cần cài: bấm X thì xuống khay hệ thống, chuột phải biểu tượng › Thoát để tắt hẳn.
// Biên dịch bằng trình biên dịch có sẵn trong Windows (C# 5): xem BUILD_EXE.bat.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;
using System.Threading.Tasks;

[assembly: System.Reflection.AssemblyTitle("Discord Mobile Status")]
[assembly: System.Reflection.AssemblyProduct("Discord Mobile Status")]
[assembly: System.Reflection.AssemblyDescription("Shows what you listen to, watch and play (YouTube, YouTube Music, mobile games) as your Discord status")]
[assembly: System.Reflection.AssemblyVersion("1.1.2.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.1.2.0")]

namespace DiscordMobileStatus
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int pid);

        [DllImport("psapi.dll")]
        static extern bool EmptyWorkingSet(IntPtr hProcess);

        /// <summary>Trả RAM không dùng tới cho Windows (gọi khi app chạy ẩn).</summary>
        public static void TrimMemory()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                using (Process p = Process.GetCurrentProcess()) EmptyWorkingSet(p.Handle);
            }
            catch (Exception) { }
        }

        [STAThread]
        static void Main(string[] args)
        {
            if (args.Length >= 2 && args[0] == "--helper") { Helper.Run(args[1]); return; }
            bool created;
            using (Mutex mutex = new Mutex(true, "DiscordMobileStatus_Mutex_v1", out created))
            {
                EventWaitHandle showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "DiscordMobileStatus_Show_v1");
                if (!created)
                {
                    try { AllowSetForegroundWindow(-1); } catch (Exception) { }
                    showEvt.Set();          // đã chạy sẵn -> bảo bản kia hiện cửa sổ lên
                    return;
                }
                try { SetProcessDPIAware(); } catch (Exception) { }
                // bản cũ (YTStatus.exe) còn chạy thì tắt đi, kẻo 2 app giành nhau trạng thái Discord
                try { foreach (Process old in Process.GetProcessesByName("YTStatus")) { old.Kill(); old.WaitForExit(2000); old.Dispose(); } } catch (Exception) { }
                try { ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; }   // TLS 1.2
                catch (NotSupportedException) { }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                bool startHidden = Array.IndexOf(args, "--an") >= 0;
                Application.Run(new MainForm(startHidden, showEvt));
                GC.KeepAlive(mutex);
            }
        }
    }

    // ================================================================ tiện ích
    static class U
    {
        public static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        // JavaScriptSerializer không an toàn khi dùng chung giữa các luồng -> mỗi lần một cái mới
        public static JavaScriptSerializer Json { get { return new JavaScriptSerializer(); } }

        public static long NowMs() { return (long)(DateTime.UtcNow - Epoch).TotalMilliseconds; }

        public static object Get(Dictionary<string, object> d, string k)
        {
            object v;
            return d != null && d.TryGetValue(k, out v) ? v : null;
        }

        public static string Str(Dictionary<string, object> d, string k)
        {
            object v = Get(d, k);
            return v == null ? null : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static bool Bool(Dictionary<string, object> d, string k)
        {
            object v = Get(d, k);
            if (v is bool) return (bool)v;
            return ToLong(v, 0, 1) == 1;
        }

        public static string Clean(object v, int n)
        {
            string s = v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
            s = Regex.Replace(s.Replace("\0", ""), @"\s+", " ").Trim();
            return s.Length > n ? s.Substring(0, n) : s;
        }

        public static long ToLong(object v, long lo, long hi)
        {
            if (v == null || v is bool) return lo;
            try
            {
                double d = Convert.ToDouble(v, CultureInfo.InvariantCulture);
                if (double.IsNaN(d)) return lo;
                d = Math.Max(lo, Math.Min(hi, d));
                return (long)d;
            }
            catch (Exception) { return lo; }
        }

        public static string Fit(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length > 128) s = s.Substring(0, 127) + "…";
            while (s.Length < 2) s += "⠀";      // Discord bắt tối thiểu 2 ký tự
            return s;
        }

        public static string HttpGet(string url, int timeoutMs)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "DiscordMobileStatus/1.1";
            req.Timeout = timeoutMs;
            req.ReadWriteTimeout = timeoutMs;
            using (WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                return sr.ReadToEnd();
        }
    }

    /// <summary>Ngôn ngữ giao diện: L.T("tiếng Việt", "English").</summary>
    static class L
    {
        public static volatile bool En;
        public static string T(string vi, string en) { return En ? en : vi; }
        public static string HiddenText()
        {
            return T("ĐANG TẮT — bật \"Chia sẻ lên Discord\"", "OFF — turn on \"Share on Discord\"");
        }
        public const string App = "Discord Mobile Status";
        public static string OnMobile() { return T("Trên điện thoại", "On mobile");
        }
    }

    static class Log
    {
        static readonly object L = new object();
        public static string PathFile;

        public static void W(string msg)
        {
            string line = DateTime.Now.ToString("dd/MM HH:mm:ss") + " " + msg;
            Debug.WriteLine(line);
            if (PathFile == null) return;
            lock (L)
            {
                try
                {
                    FileInfo fi = new FileInfo(PathFile);
                    if (fi.Exists && fi.Length > 300000)
                    {
                        string old = PathFile + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(PathFile, old);
                    }
                    File.AppendAllText(PathFile, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (Exception) { }
            }
        }
    }

    // ================================================================ cấu hình (file JSON cạnh .exe)
    class Config
    {
        public string Channel = "";
        public string Server = "https://ntfy.sh";
        // ứng dụng Discord dựng sẵn (của tác giả) — người dùng không phải tự tạo
        public const string FixedMusic = "1572104060683812985";   // "YouTube Music"
        public const string FixedVideo = "1572104134251782176";   // "YouTube"
        public const string FixedGame = "1572103833360924814";    // "Game trên điện thoại"
        public string IdMusic = FixedMusic;
        public string IdVideo = FixedVideo;
        public bool ShowPaused = false;
        public bool Hidden = false;          // "Tạm ẩn trạng thái"
        public bool Balloon = true;          // lần đầu bấm X thì nhắc "app vẫn chạy dưới khay"
        public bool UsePc = true;            // đọc YouTube đang phát trên trình duyệt của chính máy này
        public bool ShareMusic = true;       // chia sẻ bài YouTube Music lên Discord
        public bool ShareVideo = true;       // chia sẻ video YouTube lên Discord
        public string ArtTopic = "";         // kênh ntfy riêng để tải ảnh bìa từ PC lên (Discord cần link ảnh công khai)
        public string Lang = "";             // "vi" hoặc "en"; trống = theo ngôn ngữ Windows
        public string IdGame = FixedGame;    // ứng dụng Discord chung cho game điện thoại
        public bool UsePhone = true;         // nhận từ điện thoại (qua mã kênh)
        public Dictionary<string, string> GameIds = new Dictionary<string, string>();   // tên game / tên gói -> Client ID riêng
        public bool ShareGame = true;        // chia sẻ game đang chơi trên điện thoại

        public static string FilePath;

        public static void ChooseLocation()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DiscordMobileStatus");
            string local = Path.Combine(exeDir, "DiscordMobileStatus.json");
            string roam = Path.Combine(appData, "DiscordMobileStatus.json");
            // đổi tên từ YT Status: mang cấu hình cũ sang
            try
            {
                string oldLocal = Path.Combine(exeDir, "YTStatus.json");
                string oldRoam = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YTStatus"), "YTStatus.json");
                if (!File.Exists(local) && !File.Exists(roam))
                {
                    if (File.Exists(oldLocal)) File.Move(oldLocal, local);
                    else if (File.Exists(oldRoam)) { Directory.CreateDirectory(appData); File.Copy(oldRoam, roam); }
                }
                string oldLog = Path.Combine(exeDir, "YTStatus.log");
                if (File.Exists(oldLog)) File.Delete(oldLog);
                if (File.Exists(oldLog + ".1")) File.Delete(oldLog + ".1");
            }
            catch (Exception) { }
            if (File.Exists(local)) { FilePath = local; }
            else if (File.Exists(roam)) { FilePath = roam; }
            else
            {
                // ưu tiên để cạnh .exe (mang đi máy khác được); thư mục không ghi được thì dùng AppData
                try
                {
                    string probe = Path.Combine(exeDir, ".dms_probe");
                    File.WriteAllText(probe, "x");
                    File.Delete(probe);
                    FilePath = local;
                }
                catch (Exception)
                {
                    Directory.CreateDirectory(appData);
                    FilePath = roam;
                }
            }
            Log.PathFile = Path.Combine(Path.GetDirectoryName(FilePath), "DiscordMobileStatus.log");
        }

        public static Config Load()
        {
            Config c = new Config();
            try
            {
                if (!File.Exists(FilePath)) return c;
                Dictionary<string, object> d = U.Json.DeserializeObject(File.ReadAllText(FilePath, Encoding.UTF8)) as Dictionary<string, object>;
                if (d == null) return c;
                c.Channel = U.Str(d, "kenh") ?? "";
                c.Server = U.Str(d, "server") ?? c.Server;
                // Client ID cố định: bỏ qua ID cũ người dùng tự tạo trong file cấu hình
                c.ShowPaused = U.Bool(d, "hien_khi_tam_dung");
                c.Hidden = U.Bool(d, "tam_an");
                c.Balloon = U.Get(d, "nhac_khay") == null || U.Bool(d, "nhac_khay");
                c.UsePc = U.Get(d, "trinh_duyet_pc") == null || U.Bool(d, "trinh_duyet_pc");
                c.ShareMusic = U.Get(d, "chia_se_ytmusic") == null || U.Bool(d, "chia_se_ytmusic");
                c.ShareVideo = U.Get(d, "chia_se_youtube") == null || U.Bool(d, "chia_se_youtube");
                c.ArtTopic = U.Str(d, "kenh_anh") ?? "";
                c.Lang = U.Str(d, "ngon_ngu") ?? "";
                c.UsePhone = U.Get(d, "dien_thoai") == null ? c.Channel.Length > 0 : U.Bool(d, "dien_thoai");
                c.ShareGame = U.Get(d, "chia_se_game") == null || U.Bool(d, "chia_se_game");
                Dictionary<string, object> gi = U.Get(d, "client_id_game_rieng") as Dictionary<string, object>;
                if (gi != null)
                    foreach (KeyValuePair<string, object> kv in gi)
                    {
                        string v = Convert.ToString(kv.Value, CultureInfo.InvariantCulture);
                        if (Regex.IsMatch(v ?? "", @"^\d{17,20}$")) c.GameIds[kv.Key] = v;
                    }
            }
            catch (Exception e) { Log.W("Đọc cấu hình lỗi: " + e.Message); }
            return c;
        }

        public void Save()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["kenh"] = Channel;
            d["server"] = Server;
            d["dien_thoai"] = UsePhone;
            d["hien_khi_tam_dung"] = ShowPaused;
            d["tam_an"] = Hidden;
            d["nhac_khay"] = Balloon;
            d["trinh_duyet_pc"] = UsePc;
            d["chia_se_ytmusic"] = ShareMusic;
            d["chia_se_youtube"] = ShareVideo;
            d["kenh_anh"] = ArtTopic;
            d["ngon_ngu"] = Lang;

            d["chia_se_game"] = ShareGame;
            d["client_id_game_rieng"] = GameIds;
            File.WriteAllText(FilePath, U.Json.Serialize(d), new UTF8Encoding(false));
        }

        public Config Copy() { return (Config)MemberwiseClone(); }

        public string BaseUrl()
        {
            string s = (Server ?? "").Trim().TrimEnd('/');
            if (s.Length == 0) s = "https://ntfy.sh";
            if (!s.StartsWith("http://") && !s.StartsWith("https://")) s = "https://" + s;
            return s;
        }

        /// <summary>Loại này (YouTube / YouTube Music) có đang bật chia sẻ không.</summary>
        public bool Shares(Track t) { return t != null && (t.App == "youtube" ? ShareVideo : ShareMusic); }

        public string ClientIdFor(Track t)
        {
            if (t != null && t.App == "youtube" && IdVideo.Length > 0) return IdVideo;
            return IdMusic.Length > 0 ? IdMusic : IdVideo;
        }

        public bool Ready() { return true; }

        /// <summary>Client ID cho game: ID riêng của game đó (theo tên hoặc tên gói), không có thì ID chung "Game di động".</summary>
        public string GameClientIdFor(GameTrack g, out bool own)
        {
            own = false;
            if (g == null) return "";
            foreach (KeyValuePair<string, string> kv in GameIds)
            {
                if (string.Equals(kv.Key.Trim(), g.Name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(kv.Key.Trim(), g.Pkg, StringComparison.OrdinalIgnoreCase)) { own = true; return kv.Value; }
            }
            return IdGame;
        }

        public bool PhoneReady() { return UsePhone && Regex.IsMatch(Channel ?? "", "^[A-Za-z0-9_-]{6,64}$"); }

        public static string RandomTopic(string prefix)
        {
            const string a = "abcdefghjkmnpqrstuvwxyz23456789";
            byte[] b = new byte[16];
            using (System.Security.Cryptography.RandomNumberGenerator r = System.Security.Cryptography.RandomNumberGenerator.Create()) r.GetBytes(b);
            StringBuilder sb = new StringBuilder(prefix);
            foreach (byte x in b) sb.Append(a[x % a.Length]);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Tiến trình phụ (chính file exe này chạy với --helper, chỉ ngủ) để dòng "Đang chơi" có PID riêng:
    /// một số bản Discord gộp trạng thái theo PID, nếu dùng chung PID thì game sẽ đè mất dòng nhạc.
    /// Tự thoát khi app chính thoát.
    /// </summary>
    static class Helper
    {
        static Process proc;

        public static int Pid()
        {
            try
            {
                if (proc != null && !proc.HasExited) return proc.Id;
                ProcessStartInfo si = new ProcessStartInfo(Application.ExecutablePath, "--helper " + Process.GetCurrentProcess().Id);
                si.UseShellExecute = false;
                si.CreateNoWindow = true;
                proc = Process.Start(si);
                return proc.Id;
            }
            catch (Exception) { return 0; }
        }

        public static void Kill()
        {
            try { if (proc != null && !proc.HasExited) proc.Kill(); } catch (Exception) { }
        }

        /// <summary>Chạy ở tiến trình phụ: chờ app chính thoát rồi thoát theo. Gần như không tốn tài nguyên.</summary>
        public static void Run(string parentPid)
        {
            try
            {
                using (Process parent = Process.GetProcessById(int.Parse(parentPid)))
                    parent.WaitForExit();
            }
            catch (Exception) { }
        }
    }

    static class AutoStart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "Discord Mobile Status";
        const string OldName = "YT Status";

        static string Cmd() { return "\"" + Application.ExecutablePath + "\" --an"; }

        public static bool IsOn()
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key))
                return k != null && k.GetValue(Name) != null;
        }

        public static void Set(bool on)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(Key))
            {
                if (on) k.SetValue(Name, Cmd());
                else if (k.GetValue(Name) != null) k.DeleteValue(Name);
            }
        }

        /// <summary>Đã bật mà .exe bị chuyển chỗ / đổi tên -> cập nhật đường dẫn mới; chuyển mục cũ của YT Status.</summary>
        public static void Refresh()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key, true))
                {
                    if (k == null) return;
                    if (k.GetValue(OldName) != null)
                    {
                        k.DeleteValue(OldName);
                        k.SetValue(Name, Cmd());
                    }
                    object v = k.GetValue(Name);
                    if (v != null && (string)v != Cmd()) k.SetValue(Name, Cmd());
                }
            }
            catch (Exception) { }
        }
    }

    // ================================================================ bài đang phát
    class Track
    {
        public string Title = "", Artist = "", Album = "", App = "ytmusic", MediaId = "", ArtUrl = "", Cover = "";
        public long DurationMs, StartMs, ReceivedMs;
        public bool Playing;
        public string Source = "phone";      // "phone" hoặc "pc"

        public const long EndGraceMs = 45000;          // hết bài mà không có bài mới -> ẩn
        public const long PauseKeepMs = 5 * 60000;     // (khi bật "vẫn hiện khi tạm dừng") quá 5 phút -> ẩn
        public const long LiveSilentMs = 25 * 60000;   // livestream: điện thoại nhắc mỗi 10 phút

        /// <summary>Tin từ điện thoại -> bài. Trả null kèm stop=true khi điện thoại báo dừng. Tin lạ -> FormatException.</summary>
        public static Track Parse(string text, long serverTimeS, string attachmentUrl, out bool stop)
        {
            stop = false;
            Dictionary<string, object> d;
            try { d = U.Json.DeserializeObject(text) as Dictionary<string, object>; }
            catch (Exception) { throw new FormatException("không phải JSON"); }
            if (d == null || U.ToLong(U.Get(d, "v"), 0, 99) != 1) throw new FormatException("không phải tin nhạc");
            if (U.Bool(d, "stop")) { stop = true; return null; }
            Track t = new Track();
            t.Title = U.Clean(U.Get(d, "t"), 128);
            if (t.Title.Length == 0) throw new FormatException("thiếu tên bài");
            t.Artist = U.Clean(U.Get(d, "a"), 128);
            t.Album = U.Clean(U.Get(d, "al"), 128);
            t.App = U.Str(d, "app") == "youtube" ? "youtube" : "ytmusic";
            string id = U.Clean(U.Get(d, "id"), 11);
            t.MediaId = Regex.IsMatch(id, "^[A-Za-z0-9_-]{11}$") ? id : "";
            long maxMs = 48L * 3600 * 1000;
            t.DurationMs = U.ToLong(U.Get(d, "d"), 0, maxMs);
            long pos = U.ToLong(U.Get(d, "p"), 0, maxMs);
            if (t.DurationMs > 0 && pos > t.DurationMs) pos = t.DurationMs;
            t.Playing = U.Bool(d, "pl");
            long now = U.NowMs();
            long sent = U.ToLong(U.Get(d, "ts"), 0, 100000000000000L);
            if (sent == 0) sent = now;
            // đồng hồ điện thoại lệch quá 10 giây -> dùng giờ của ntfy
            if (serverTimeS > 0 && Math.Abs(serverTimeS * 1000 - sent) > 10000) sent = serverTimeS * 1000;
            t.StartMs = sent - pos;
            t.ReceivedMs = now;
            string art = attachmentUrl ?? U.Clean(U.Get(d, "art"), 300);
            t.ArtUrl = art.StartsWith("https://") ? art : "";
            return t;
        }

        public bool Expired(bool showPaused, long now)
        {
            if (!Playing) return !showPaused || now - ReceivedMs > PauseKeepMs;
            if (DurationMs > 0) return now > StartMs + DurationMs + EndGraceMs;
            return now - ReceivedMs > LiveSilentMs;
        }

        public string Label()
        {
            return Title + (Artist.Length > 0 ? " — " + Artist : "");
        }

        static readonly Dictionary<string, string> ItunesCache = new Dictionary<string, string>();

        /// <summary>Ảnh bìa: ảnh từ điện thoại > ảnh thu nhỏ video YouTube > bìa album trên iTunes.</summary>
        public void FindCover()
        {
            if (ArtUrl.Length > 0) { Cover = ArtUrl; return; }
            if (MediaId.Length > 0) { Cover = "https://i.ytimg.com/vi/" + MediaId + "/hqdefault.jpg"; return; }
            string key = Title + "\n" + Artist;
            lock (ItunesCache)
            {
                string c;
                if (ItunesCache.TryGetValue(key, out c)) { Cover = c; return; }
            }
            string url = "";
            try
            {
                string q = Uri.EscapeDataString(Title + " " + Artist);
                string body = U.HttpGet("https://itunes.apple.com/search?entity=song&limit=1&term=" + q, 6000);
                Dictionary<string, object> r = U.Json.DeserializeObject(body) as Dictionary<string, object>;
                object[] res = U.Get(r, "results") as object[];
                if (res == null)
                {
                    System.Collections.ArrayList al = U.Get(r, "results") as System.Collections.ArrayList;
                    if (al != null) res = al.ToArray();
                }
                if (res != null && res.Length > 0)
                {
                    string a = U.Str(res[0] as Dictionary<string, object>, "artworkUrl100");
                    if (!string.IsNullOrEmpty(a)) url = a.Replace("100x100bb", "512x512bb");
                }
            }
            catch (Exception) { }
            lock (ItunesCache)
            {
                if (ItunesCache.Count > 300) ItunesCache.Clear();
                ItunesCache[key] = url;
            }
            Cover = url;
        }

        /// <summary>mode: "full" (đầy đủ), "plain" (bỏ trường mới), "playing" (đổi sang Đang chơi cho Discord rất cũ).</summary>
        public Dictionary<string, object> Activity(string mode)
        {
            bool music = App != "youtube";
            string appName = music ? "YouTube Music" : "YouTube";
            string who = Artist.Length > 0 ? Artist : appName;
            Dictionary<string, object> act = new Dictionary<string, object>();
            act["type"] = mode == "playing" ? 0 : (music ? 2 : 3);     // 2 = Đang nghe, 3 = Đang xem
            act["details"] = U.Fit(Title);
            act["state"] = Playing ? U.Fit(who) : U.Fit(L.T("Tạm dừng · ", "Paused · ") + who);
            Dictionary<string, object> assets = new Dictionary<string, object>();
            assets["small_image"] = music ? "ytmusic" : "youtube";      // tên ảnh ở Developer Portal › Rich Presence › Art Assets
            assets["small_text"] = appName;
            string cover = Cover.Length > 0 ? Cover : ArtUrl;
            if (cover.Length > 0)
            {
                assets["large_image"] = cover;
                assets["large_text"] = U.Fit(Album.Length > 0 ? Album : Title);
            }
            act["assets"] = assets;
            act["instance"] = false;
            if (mode == "full") act["status_display_type"] = 2;       // danh sách thành viên hiện "Đang nghe <tên bài>"
            if (Playing)
            {
                Dictionary<string, object> ts = new Dictionary<string, object>();
                ts["start"] = StartMs;
                if (DurationMs > 0) ts["end"] = StartMs + DurationMs;
                act["timestamps"] = ts;
            }
            return act;
        }

        public string Signature()
        {
            return string.Join("|", new string[] { Title, Artist, App, Playing.ToString(), Cover, StartMs.ToString(), DurationMs.ToString() });
        }

        public static Track Demo()
        {
            Track t = new Track();
            t.Title = L.T("Bài thử của Discord Mobile Status", "Discord Mobile Status test track");
            t.Artist = L.App;
            t.Album = L.T("Thử", "Test");
            t.Playing = true;
            t.DurationMs = 200000;
            t.StartMs = U.NowMs() - 30000;
            t.ReceivedMs = U.NowMs();
            t.Cover = "https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg";
            return t;
        }
    }


    // ================================================================ game trên điện thoại
    class GameTrack
    {
        public string Pkg = "", Name = "", Icon = "";
        public long StartMs, ReceivedMs;
        public const long SilentMs = 25 * 60000;     // điện thoại nhắc mỗi 10 phút; im 25 phút -> coi như đã thoát

        /// <summary>Tin game {"v":2,"k":"game",...}. Trả null + stop=true khi điện thoại báo thoát game.</summary>
        public static GameTrack Parse(Dictionary<string, object> d, string attachmentUrl, long serverTimeS, out bool stop)
        {
            stop = false;
            if (U.Bool(d, "stop")) { stop = true; return null; }
            GameTrack g = new GameTrack();
            g.Name = U.Clean(U.Get(d, "n"), 100);
            if (g.Name.Length == 0) throw new FormatException("thiếu tên game");
            g.Pkg = U.Clean(U.Get(d, "pkg"), 200);
            long now = U.NowMs();
            long sent = U.ToLong(U.Get(d, "ts"), 0, 100000000000000L);
            long skew = (serverTimeS > 0 && sent > 0 && Math.Abs(serverTimeS * 1000 - sent) > 10000) ? serverTimeS * 1000 - sent : 0;
            g.StartMs = U.ToLong(U.Get(d, "st"), 0, 100000000000000L) + skew;
            if (g.StartMs <= 0 || g.StartMs > now + 60000 || now - g.StartMs > 24L * 3600 * 1000) g.StartMs = now;
            string icon = attachmentUrl ?? U.Clean(U.Get(d, "icon"), 300);
            g.Icon = icon.StartsWith("https://") ? icon : "";
            g.ReceivedMs = serverTimeS > 0 ? Math.Min(now, serverTimeS * 1000) : now;
            return g;
        }

        public bool Expired(long now) { return now - ReceivedMs > SilentMs; }

        public string Signature() { return Pkg + "|" + Name + "|" + StartMs + "|" + Icon; }

        /// <param name="own">true = ứng dụng Discord mang đúng tên game (khỏi ghi tên game lần nữa).</param>
        /// <param name="rich">false = bỏ trường mới cho Discord bản cũ.</param>
        public Dictionary<string, object> Activity(bool own, bool rich)
        {
            Dictionary<string, object> act = new Dictionary<string, object>();
            act["type"] = 0;                                  // 0 = Đang chơi
            if (own) act["details"] = U.Fit(L.OnMobile());
            else
            {
                act["details"] = U.Fit(Name);
                act["state"] = U.Fit(L.OnMobile());
                if (rich) act["status_display_type"] = 2;      // danh sách thành viên hiện "Đang chơi <tên game>"
            }
            if (Icon.Length > 0)
            {
                Dictionary<string, object> assets = new Dictionary<string, object>();
                assets["large_image"] = Icon;
                assets["large_text"] = U.Fit(Name);
                act["assets"] = assets;
            }
            Dictionary<string, object> ts = new Dictionary<string, object>();
            ts["start"] = StartMs;
            act["timestamps"] = ts;
            act["instance"] = false;
            return act;
        }
    }

    // ================================================================ YouTube trên trình duyệt của PC
    // Windows cho app đọc "đang phát gì" của mọi trình duyệt (bảng điều khiển media của Windows).
    // Nhận ra YouTube / YouTube Music nhờ tiêu đề cửa sổ trình duyệt.
    static class PcMedia
    {
        // không dùng Environment.OSVersion: exe không khai báo Windows 10 sẽ bị báo là Windows 8 (6.2)
        static int supported = -1;
        public static bool Supported
        {
            get
            {
                if (supported < 0)
                {
                    try { supported = Type.GetType(MgrType, false) != null ? 1 : 0; }
                    catch (Exception) { supported = 0; }
                }
                return supported == 1;
            }
        }

        const string MgrType = "Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager, Windows.Media.Control, ContentType=WindowsRuntime";

        public class Info
        {
            public string Title = "", Artist = "", Album = "", App = "youtube";
            public long DurationMs, PositionMs;
            public bool Playing;
            public byte[] Thumb;
            public string ThumbType = "image/png";
        }

        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr lp);
        [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int max);
        [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
        delegate bool EnumProc(IntPtr h, IntPtr lp);

        static List<string> WindowTitles()
        {
            List<string> list = new List<string>();
            EnumWindows(delegate (IntPtr h, IntPtr lp)
            {
                if (!IsWindowVisible(h)) return true;
                int n = GetWindowTextLength(h);
                if (n <= 0 || n > 1000) return true;
                StringBuilder sb = new StringBuilder(n + 1);
                GetWindowText(h, sb, sb.Capacity);
                string t = sb.ToString();
                if (t.IndexOf("- YouTube", StringComparison.OrdinalIgnoreCase) >= 0
                    || t.IndexOf("YouTube Music", StringComparison.OrdinalIgnoreCase) >= 0) list.Add(t);
                return true;
            }, IntPtr.Zero);
            return list;
        }

        static readonly Dictionary<string, string> AppCache = new Dictionary<string, string>();   // tên bài -> youtube/ytmusic

        /// <summary>Tên bài đang phát thuộc YouTube hay YouTube Music? null = không nhận ra.</summary>
        // trình phát nhạc riêng (không phải trình duyệt) -> không bao giờ coi là YouTube
        static readonly string[] NotBrowser = { "spotify", "zunemusic", "zunevideo", "microsoft.media", "vlc", "foobar", "aimp",
                                                "itunes", "musicbee", "winamp", "potplayer", "mpc-hc", "groove", "tidal", "deezer" };

        static readonly string[] Browsers = { "chrome", "msedge", "edge", "firefox", "opera", "brave", "vivaldi", "coccoc", "browser", "arc" };

        static bool IsBrowser(string src)
        {
            foreach (string b in Browsers) if (src.Contains(b)) return true;
            return false;
        }

        // cùng bài thì khỏi quét lại các cửa sổ liên tục: quét lại sau 10 s (đã nhận ra) / 4 s (chưa nhận ra)
        static string lastKey;
        static string lastApp;
        static DateTime lastScan = DateTime.MinValue;

        static string Classify(string title, string sourceId)
        {
            string key = title + "\n" + sourceId;
            if (key == lastKey && (DateTime.UtcNow - lastScan).TotalSeconds < (lastApp != null ? 10 : 4)) return lastApp;
            lastKey = key;
            lastScan = DateTime.UtcNow;
            lastApp = ClassifyNow(title, sourceId);
            return lastApp;
        }

        static string ClassifyNow(string title, string sourceId)
        {
            string src = (sourceId ?? "").ToLowerInvariant();
            foreach (string n in NotBrowser) if (src.Contains(n)) return null;
            List<string> wins = WindowTitles();
            string app = null;
            foreach (string w in wins)
            {
                if (title.Length > 0 && w.IndexOf(title, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    app = w.IndexOf("YouTube Music", StringComparison.OrdinalIgnoreCase) >= 0 ? "ytmusic" : "youtube";
                    break;
                }
            }
            if (app == null && (sourceId ?? "").IndexOf("cinhimbnkkaeohfgghhklpknlkffjgod", StringComparison.OrdinalIgnoreCase) >= 0) app = "ytmusic";   // app YouTube Music cài từ Chrome
            if (app == null)
                foreach (string w in wins)
                    if (w.IndexOf("YouTube Music", StringComparison.OrdinalIgnoreCase) >= 0) { app = "ytmusic"; break; }
            if (app == null && wins.Count > 0 && IsBrowser(src)) app = "youtube";
            lock (AppCache)
            {
                if (app != null)
                {
                    if (AppCache.Count > 300) AppCache.Clear();
                    AppCache[title] = app;
                }
                else AppCache.TryGetValue(title, out app);     // tab YouTube bị che -> nhớ lần trước
            }
            return app;
        }

        // Thư viện media của Windows (WinRT) được nạp lúc chạy bằng reflection, nên build không cần tham chiếu thêm gì.
        static Type tMgr, tSession, tProps;
        static MethodInfo asTaskGeneric, asStreamForRead;
        static object mgr;
        static MethodInfo getEnum;

        static void Init()
        {
            if (mgr != null) return;
            tMgr = Type.GetType(MgrType, true);
            Assembly rt;
            try { rt = Assembly.Load("System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"); }
            catch (Exception)
            {
                rt = Assembly.LoadFrom(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "System.Runtime.WindowsRuntime.dll"));
            }
            foreach (MethodInfo m in rt.GetType("System.WindowsRuntimeSystemExtensions", true).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                ParameterInfo[] ps = m.GetParameters();
                if (m.Name == "AsTask" && m.IsGenericMethodDefinition && ps.Length == 1 && ps[0].ParameterType.Name == "IAsyncOperation`1")
                {
                    asTaskGeneric = m;
                    break;
                }
            }
            if (asTaskGeneric == null) throw new NotSupportedException("thiếu AsTask");
            foreach (MethodInfo m in rt.GetType("System.IO.WindowsRuntimeStreamExtensions", true).GetMethods(BindingFlags.Public | BindingFlags.Static))
                if (m.Name == "AsStreamForRead" && m.GetParameters().Length == 1) { asStreamForRead = m; break; }
            MethodInfo gs = tMgr.GetMethod("GetSessions", Type.EmptyTypes);
            tSession = gs.ReturnType.GetGenericArguments()[0];
            tProps = tSession.GetMethod("TryGetMediaPropertiesAsync", Type.EmptyTypes).ReturnType.GetGenericArguments()[0];
            MethodInfo req = tMgr.GetMethod("RequestAsync", BindingFlags.Public | BindingFlags.Static);
            mgr = Await(req.Invoke(null, null), req.ReturnType);
            Log.W("Đã nối bảng điều khiển media của Windows");
        }

        /// <summary>Chờ một IAsyncOperation&lt;T&gt; của WinRT (tối đa 5 giây).</summary>
        static object Await(object op, Type opType)
        {
            Type res = opType.GetGenericArguments()[0];
            MethodInfo at;
            lock (awaitCache)
            {
                if (!awaitCache.TryGetValue(res, out at)) { at = asTaskGeneric.MakeGenericMethod(res); awaitCache[res] = at; }
            }
            Task t = (Task)at.Invoke(null, new object[] { op });
            if (!t.Wait(5000)) throw new TimeoutException("Windows không trả lời");
            Type tt = t.GetType();
            PropertyInfo rp;
            lock (resultCache)
            {
                if (!resultCache.TryGetValue(tt, out rp)) { rp = tt.GetProperty("Result"); resultCache[tt] = rp; }
            }
            return rp.GetValue(t, null);
        }

        // tra cứu reflection chỉ làm 1 lần cho mỗi (kiểu, tên) rồi nhớ lại -> mỗi lần đọc rất nhẹ
        static readonly Dictionary<string, PropertyInfo> propCache = new Dictionary<string, PropertyInfo>();
        static readonly Dictionary<string, MethodInfo> methCache = new Dictionary<string, MethodInfo>();
        static readonly Dictionary<Type, MethodInfo> awaitCache = new Dictionary<Type, MethodInfo>();
        static readonly Dictionary<Type, PropertyInfo> resultCache = new Dictionary<Type, PropertyInfo>();

        /// <summary>Tìm thuộc tính trên kiểu hoặc các interface cha của nó.</summary>
        static PropertyInfo FindProp(Type t, string name)
        {
            string key = t.FullName + "|" + name;
            PropertyInfo p;
            lock (propCache) { if (propCache.TryGetValue(key, out p)) return p; }
            p = t.GetProperty(name);
            if (p == null)
            {
                foreach (Type i in t.GetInterfaces())
                {
                    p = i.GetProperty(name);
                    if (p != null) break;
                }
            }
            lock (propCache) propCache[key] = p;
            return p;
        }

        static object Get(object o, Type t, string name)
        {
            PropertyInfo p = FindProp(t, name);
            return p == null ? null : p.GetValue(o, null);
        }

        static object Call(object o, Type t, string name, out Type ret)
        {
            string key = t.FullName + "|" + name;
            MethodInfo m;
            lock (methCache)
            {
                if (!methCache.TryGetValue(key, out m))
                {
                    m = t.GetMethod(name, Type.EmptyTypes);
                    methCache[key] = m;
                }
            }
            ret = m.ReturnType;
            return m.Invoke(o, null);
        }

        static string S(object o, Type t, string name)
        {
            object v = Get(o, t, name);
            return v == null ? "" : v.ToString();
        }

        /// <summary>Bài YouTube đang phát (hoặc tạm dừng) trên PC; null nếu không có.</summary>
        public static Info Read(bool wantThumb, out string note)
        {
            note = null;
            Init();
            Type listType;
            object list = Call(mgr, tMgr, "GetSessions", out listType);
            if (getEnum == null) getEnum = typeof(IEnumerable<>).MakeGenericType(tSession).GetMethod("GetEnumerator");
            System.Collections.IEnumerator en = (System.Collections.IEnumerator)getEnum.Invoke(list, null);
            Info best = null;
            while (en.MoveNext())
            {
                object s = en.Current;
                object p;
                bool playing;
                try
                {
                    Type opT;
                    object op = Call(s, tSession, "TryGetMediaPropertiesAsync", out opT);
                    p = Await(op, opT);
                    Type piT;
                    object pi = Call(s, tSession, "GetPlaybackInfo", out piT);
                    playing = S(pi, piT, "PlaybackStatus") == "Playing";
                }
                catch (Exception) { continue; }
                string title = S(p, tProps, "Title").Trim();
                if (title.Length == 0) continue;
                if (best != null && (best.Playing || !playing)) continue;
                string app = Classify(title, S(s, tSession, "SourceAppUserModelId"));
                if (app == null)
                {
                    if (playing) note = L.T("Đang phát \"" + U.Clean(title, 40) + "\" nhưng không phải tab YouTube", "Playing \"" + U.Clean(title, 40) + "\" — not a YouTube tab");
                    continue;
                }
                Info i = new Info();
                i.Title = U.Clean(title, 128);
                i.Artist = U.Clean(S(p, tProps, "Artist"), 128);
                i.Album = U.Clean(S(p, tProps, "AlbumTitle"), 128);
                i.App = app;
                i.Playing = playing;
                try
                {
                    Type tlT;
                    object tl = Call(s, tSession, "GetTimelineProperties", out tlT);
                    TimeSpan st = (TimeSpan)Get(tl, tlT, "StartTime");
                    TimeSpan et = (TimeSpan)Get(tl, tlT, "EndTime");
                    TimeSpan ps = (TimeSpan)Get(tl, tlT, "Position");
                    DateTimeOffset lu = (DateTimeOffset)Get(tl, tlT, "LastUpdatedTime");
                    long dur = (long)(et - st).TotalMilliseconds;
                    long pos = (long)(ps - st).TotalMilliseconds;
                    if (playing && lu.Year > 2000)
                    {
                        double gap = (DateTimeOffset.Now - lu).TotalMilliseconds;
                        if (gap > 0 && gap < 6 * 3600 * 1000) pos += (long)gap;
                    }
                    if (dur > 0 && dur < 48L * 3600 * 1000)
                    {
                        i.DurationMs = dur;
                        i.PositionMs = Math.Max(0, Math.Min(pos, dur));
                    }
                }
                catch (Exception) { }
                if (wantThumb && asStreamForRead != null)
                {
                    try
                    {
                        PropertyInfo thP = FindProp(tProps, "Thumbnail");
                        object thumb = thP == null ? null : thP.GetValue(p, null);
                        if (thumb != null)
                        {
                            MethodInfo open = thP.PropertyType.GetMethod("OpenReadAsync", Type.EmptyTypes);
                            object stream = Await(open.Invoke(thumb, null), open.ReturnType);
                            Type stT = open.ReturnType.GetGenericArguments()[0];
                            string ctype = S(stream, stT, "ContentType");
                            using (Stream net = (Stream)asStreamForRead.Invoke(null, new object[] { stream }))
                            using (MemoryStream ms = new MemoryStream())
                            {
                                byte[] buf = new byte[16384];
                                int n;
                                while ((n = net.Read(buf, 0, buf.Length)) > 0)
                                {
                                    ms.Write(buf, 0, n);
                                    if (ms.Length > 1800000) break;
                                }
                                if (ms.Length > 100 && ms.Length <= 1800000)
                                {
                                    i.Thumb = ms.ToArray();
                                    i.ThumbType = ctype.Length > 0 ? ctype : "image/png";
                                }
                            }
                            IDisposable d = stream as IDisposable;
                            if (d != null) d.Dispose();
                        }
                    }
                    catch (Exception) { }
                }
                best = i;
            }
            return best;
        }
    }

    // ================================================================ Discord (IPC cục bộ, cách hợp lệ)
    class RpcException : Exception { public RpcException(string m) : base(m) { } }
    class ActivityRejected : Exception { public ActivityRejected(string m) : base(m) { } }

    class DiscordIpc
    {
        NamedPipeClientStream pipe;
        public readonly string ClientId;
        public string User = "";
        readonly int pid;

        public DiscordIpc(string clientId) : this(clientId, 0) { }

        /// <param name="pidOverride">PID báo cho Discord (0 = PID của app). Kết nối game dùng PID tiến trình phụ.</param>
        public DiscordIpc(string clientId, int pidOverride)
        {
            ClientId = clientId;
            pid = pidOverride > 0 ? pidOverride : Process.GetCurrentProcess().Id;
        }

        public bool Ok { get { return pipe != null && pipe.IsConnected; } }

        public void Connect()
        {
            for (int i = 0; i < 10 && pipe == null; i++)
            {
                NamedPipeClientStream p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut);
                try { p.Connect(150); pipe = p; }
                catch (Exception) { p.Dispose(); }
            }
            if (pipe == null) throw new RpcException(L.T("Discord chưa mở", "Discord is not open"));
            Dictionary<string, object> hs = new Dictionary<string, object>();
            hs["v"] = 1;
            hs["client_id"] = ClientId;
            int op;
            Dictionary<string, object> d;
            try
            {
                Send(0, hs);
                d = Recv(out op);
            }
            catch (IOException e) { Close(); throw new RpcException(e.Message); }
            if (op == 2 || U.Str(d, "evt") != "READY")
            {
                Close();
                throw new RpcException(L.T("Discord từ chối Client ID ", "Discord rejected Client ID ") + ClientId + ": " + (U.Str(d, "message") ?? "?"));
            }
            Dictionary<string, object> user = U.Get(U.Get(d, "data") as Dictionary<string, object>, "user") as Dictionary<string, object>;
            User = U.Str(user, "global_name") ?? U.Str(user, "username") ?? "?";
        }

        public void Close()
        {
            try { if (pipe != null) pipe.Dispose(); } catch (Exception) { }
            pipe = null;
        }

        void Send(int op, object obj)
        {
            byte[] body = Encoding.UTF8.GetBytes(U.Json.Serialize(obj));
            byte[] buf = new byte[8 + body.Length];
            BitConverter.GetBytes(op).CopyTo(buf, 0);
            BitConverter.GetBytes(body.Length).CopyTo(buf, 4);
            body.CopyTo(buf, 8);
            pipe.Write(buf, 0, buf.Length);
            pipe.Flush();
        }

        byte[] ReadN(int n)
        {
            byte[] b = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = pipe.Read(b, got, n - got);
                if (r <= 0) throw new IOException(L.T("Discord đóng kết nối", "Discord closed the connection"));
                got += r;
            }
            return b;
        }

        Dictionary<string, object> Recv(out int op)
        {
            byte[] h = ReadN(8);
            op = BitConverter.ToInt32(h, 0);
            int len = BitConverter.ToInt32(h, 4);
            if (len < 0 || len > 10000000) throw new IOException(L.T("khung tin hỏng", "bad frame"));
            if (len == 0) return new Dictionary<string, object>();
            string s = Encoding.UTF8.GetString(ReadN(len));
            return (U.Json.DeserializeObject(s) as Dictionary<string, object>) ?? new Dictionary<string, object>();
        }

        public void SetActivity(Dictionary<string, object> activity)
        {
            if (!Ok) throw new RpcException(L.T("chưa nối Discord", "not connected to Discord"));
            Dictionary<string, object> args = new Dictionary<string, object>();
            args["pid"] = pid;
            args["activity"] = activity;
            string nonce = Guid.NewGuid().ToString();
            Dictionary<string, object> frame = new Dictionary<string, object>();
            frame["cmd"] = "SET_ACTIVITY";
            frame["args"] = args;
            frame["nonce"] = nonce;
            Dictionary<string, object> d;
            try
            {
                Send(1, frame);
                while (true)
                {
                    int op;
                    d = Recv(out op);
                    if (op == 3) { Send(4, d); continue; }                 // ping -> pong
                    if (op == 2) throw new IOException(L.T("Discord đóng kết nối: ", "Discord closed the connection: ") + U.Str(d, "message"));
                    if (U.Str(d, "nonce") == nonce) break;
                }
            }
            catch (IOException e) { Close(); throw new RpcException(e.Message); }
            catch (InvalidOperationException e) { Close(); throw new RpcException(e.Message); }
            if (U.Str(d, "evt") == "ERROR")
                throw new ActivityRejected(U.Str(U.Get(d, "data") as Dictionary<string, object>, "message") ?? "error");
        }
    }

    // ================================================================ lõi: nghe ntfy + đặt trạng thái Discord
    class Engine
    {
        public volatile Config Cfg;
        public event Action<string, string, string, string> StatusChanged;   // điện thoại, máy tính, Discord, đang hiện
        public event Action<string> GameStatusChanged;                       // dòng "Game"

        readonly object trackLock = new object();
        readonly object rpcLock = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        Track track;          // từ điện thoại
        Track pcTrack;        // từ trình duyệt trên PC
        GameTrack game;       // game đang chơi trên điện thoại
        bool replaying;       // đang đọc lại tin cũ lúc khởi động (bỏ qua tìm ảnh bìa)
        DiscordIpc rpc;
        // game hiện bằng một kết nối Discord RIÊNG (ứng dụng khác) -> hiện song song với bài nhạc
        DiscordIpc grpc;
        string gShownSig;
        DateTime gLastSet = DateTime.MinValue;
        bool gRich = true;
        Func<string> stGame = () => "—";
        string lastGameEmit;
        string shownSig;
        DateTime lastSet = DateTime.MinValue;
        string fallback;
        long demoUntil;
        volatile bool stopping;
        int gen;
        HttpWebRequest currentReq;
        // mỗi dòng trạng thái lưu dạng hàm -> đổi ngôn ngữ là vẽ lại đúng ngôn ngữ mới
        Func<string> stPhone = () => L.T("Đang kết nối…", "Connecting…"), stPc = () => "—", stDiscord = () => "—",
                     stNow = () => L.T("Không có gì", "Nothing");
        volatile bool forceReapply;

        public Engine(Config cfg) { Cfg = cfg; }

        public void Start()
        {
            // các luồng nền chạy ưu tiên thấp, không giành CPU với game / trình duyệt
            Thread t1 = new Thread(ListenLoop); t1.IsBackground = true; t1.Name = "ntfy"; t1.Priority = ThreadPriority.BelowNormal; t1.Start();
            Thread t2 = new Thread(WorkLoop); t2.IsBackground = true; t2.Name = "discord"; t2.Priority = ThreadPriority.BelowNormal; t2.Start();
            Thread t3 = new Thread(PcLoop); t3.IsBackground = true; t3.Name = "pc"; t3.Priority = ThreadPriority.Lowest; t3.Start();
        }

        public void Reconfigure(Config c)
        {
            Cfg = c;
            Interlocked.Increment(ref gen);
            HttpWebRequest r = currentReq;
            if (r != null) { try { r.Abort(); } catch (Exception) { } }
            wake.Set();
        }

        public void Poke() { wake.Set(); }

        public void Demo()
        {
            demoUntil = U.NowMs() + 20000;
            wake.Set();
        }

        public void Stop()
        {
            stopping = true;
            HttpWebRequest r = currentReq;
            if (r != null) { try { r.Abort(); } catch (Exception) { } }
            // luồng Discord có thể đang kẹt chờ Discord trả lời -> không chờ quá 1,5 giây, tránh treo khi Thoát
            if (Monitor.TryEnter(rpcLock, 1500))
            {
                try
                {
                    // không xóa trạng thái bằng lệnh (Discord treo thì sẽ kẹt): đóng ống là Discord tự xóa
                    if (rpc != null) { rpc.Close(); rpc = null; }
                    if (grpc != null) { grpc.Close(); grpc = null; }
                }
                finally { Monitor.Exit(rpcLock); }
            }
            Helper.Kill();
        }

        void Status(Func<string> phone, Func<string> discord, Func<string> now) { Status4(phone, null, discord, now); }

        void Status4(Func<string> phone, Func<string> pc, Func<string> discord, Func<string> now)
        {
            if (phone != null) stPhone = phone;
            if (pc != null) stPc = pc;
            if (discord != null) stDiscord = discord;
            if (now != null) stNow = now;
            Emit();
        }

        /// <summary>Gửi lại 4 dòng trạng thái cho cửa sổ (theo ngôn ngữ hiện tại).</summary>
        string lastEmit;

        public void Emit() { Emit(false); }

        void Emit(bool force)
        {
            Action<string, string, string, string> h = StatusChanged;
            if (h == null) return;
            string a = stPhone(), b = stPc(), c = stDiscord(), d = stNow();
            string sig = a + "\u0001" + b + "\u0001" + c + "\u0001" + d;
            if (!force && sig == lastEmit) return;      // không đổi -> khỏi vẽ lại
            lastEmit = sig;
            h(a, b, c, d);
        }

        /// <summary>Đổi ngôn ngữ: vẽ lại trạng thái + đặt lại dòng Discord (chữ "Tạm dừng").</summary>
        public void Relabel()
        {
            forceReapply = true;
            Emit(true);
            EmitGame(true);
            wake.Set();
        }

        /// <summary>Đang phát trên PC &gt; đang phát trên điện thoại &gt; tạm dừng trên PC &gt; tạm dừng trên điện thoại.</summary>
        Track Pick() { return Pick(true); }

        GameTrack PickGame()
        {
            Config c = Cfg;
            lock (trackLock)
            {
                if (game == null || game.Expired(U.NowMs()) || !c.ShareGame || c.Hidden) return null;
                return game;
            }
        }

        /// <param name="onlyShared">true = bỏ qua loại đang tắt chia sẻ (YouTube / YouTube Music).</param>
        Track Pick(bool onlyShared)
        {
            Config c = Cfg;
            long now = U.NowMs();
            lock (trackLock)
            {
                Track pc = pcTrack != null && !pcTrack.Expired(c.ShowPaused, now) && (!onlyShared || c.Shares(pcTrack)) ? pcTrack : null;
                Track ph = track != null && !track.Expired(c.ShowPaused, now) && (!onlyShared || c.Shares(track)) ? track : null;
                if (pc != null && pc.Playing) return pc;
                if (ph != null && ph.Playing) return ph;
                return pc ?? ph;
            }
        }

        // ------------------------------------------------ trình duyệt trên PC
        readonly Dictionary<string, KeyValuePair<string, DateTime>> artUrls = new Dictionary<string, KeyValuePair<string, DateTime>>();   // bài -> (link ảnh, lúc tải)

        void PcLoop()
        {
            bool failedOnce = false;
            int idle = 0;                       // số lần liền không thấy YouTube -> đọc thưa dần (2 s ... 8 s)
            while (!stopping)
            {
                Thread.Sleep(idle < 5 ? 2000 : idle < 15 ? 4000 : 8000);
                Config c = Cfg;
                if (!c.UsePc || !PcMedia.Supported)
                {
                    idle = 0;
                    bool had;
                    lock (trackLock) { had = pcTrack != null; pcTrack = null; }
                    if (had) wake.Set();
                    bool sup = PcMedia.Supported;
                    Status4(null, () => sup ? L.T("Đang tắt", "Off") : L.T("Cần Windows 10 bản 1809 trở lên", "Needs Windows 10 1809 or newer"), null, null);
                    Thread.Sleep(3000);
                    continue;
                }
                try
                {
                    Track old;
                    lock (trackLock) old = pcTrack;
                    string note;
                    PcMedia.Info i = PcMedia.Read(false, out note);
                    failedOnce = false;
                    if (i == null || !i.Playing) idle++; else idle = 0;
                    if (i == null)
                    {
                        if (old != null)
                        {
                            lock (trackLock) pcTrack = null;
                            Log.W("Máy tính: (dừng)");
                            wake.Set();
                        }
                        string n2 = note;
                        Status4(null, () => n2 ?? L.T("Không phát YouTube", "No YouTube playing"), null, null);
                        continue;
                    }
                    long now = U.NowMs();
                    long start = now - i.PositionMs;
                    bool same = old != null && old.Title == i.Title && old.Artist == i.Artist && old.App == i.App
                                && old.Playing == i.Playing && old.DurationMs == i.DurationMs
                                && (!i.Playing || Math.Abs(old.StartMs - start) < 5000);
                    if (same)
                    {
                        if (old.Playing) old.ReceivedMs = now;      // vẫn đang phát bài cũ
                        continue;
                    }
                    Track t = new Track();
                    t.Source = "pc";
                    t.Title = i.Title;
                    t.Artist = i.Artist;
                    t.Album = i.Album;
                    t.App = i.App;
                    t.Playing = i.Playing;
                    t.DurationMs = i.DurationMs;
                    t.StartMs = start;
                    t.ReceivedMs = now;
                    bool newSong = old == null || old.Title != i.Title || old.Artist != i.Artist;
                    t.Cover = newSong ? "" : old.Cover;
                    lock (trackLock) pcTrack = t;
                    Log.W("Máy tính: " + t.Label() + (t.Playing ? "" : " [tạm dừng]"));
                    Status4(null, () => (t.Playing ? L.T("Đang phát: ", "Playing: ") : L.T("Tạm dừng: ", "Paused: ")) + t.Label(), null, null);
                    wake.Set();
                    if (newSong)
                    {
                        string url = CoverForPc(c, t);
                        if (url.Length > 0)
                        {
                            lock (trackLock) { if (pcTrack == t) t.Cover = url; }
                            wake.Set();
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!failedOnce) Log.W("Đọc trình duyệt lỗi: " + e.GetBaseException().GetType().Name + ": " + e.GetBaseException().Message);
                    failedOnce = true;
                    Status4(null, () => L.T("Lỗi đọc trình duyệt — xem DiscordMobileStatus.log", "Cannot read the browser — see DiscordMobileStatus.log"), null, null);
                    Thread.Sleep(10000);
                }
            }
        }

        /// <summary>Ảnh bìa cho bài trên PC: ảnh của trình duyệt (tải lên ntfy để có link công khai), không có thì tìm iTunes.</summary>
        string CoverForPc(Config c, Track t)
        {
            string key = t.App + "\n" + t.Title + "\n" + t.Artist;
            lock (artUrls)
            {
                KeyValuePair<string, DateTime> u;
                if (artUrls.TryGetValue(key, out u) && (DateTime.UtcNow - u.Value).TotalMinutes < 150) return u.Key;
            }
            string url = "";
            try
            {
                Thread.Sleep(800);       // trình duyệt hay gửi ảnh chậm hơn tên bài một chút
                string note;
                PcMedia.Info i = PcMedia.Read(true, out note);
                if (i != null && i.Thumb != null && i.Title == t.Title && c.ArtTopic.Length > 0)
                    url = UploadArt(c, i.Thumb, i.ThumbType);
            }
            catch (Exception e) { Log.W("Tải ảnh bìa lỗi: " + e.GetBaseException().Message); }
            if (url.Length == 0 && t.App == "ytmusic") { t.FindCover(); url = t.Cover; }
            lock (artUrls)
            {
                if (artUrls.Count > 300) artUrls.Clear();
                artUrls[key] = new KeyValuePair<string, DateTime>(url, DateTime.UtcNow);
            }
            return url;
        }

        static string UploadArt(Config c, byte[] img, string type)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(c.BaseUrl() + "/" + Uri.EscapeDataString(c.ArtTopic));
            req.Method = "PUT";
            req.UserAgent = "DiscordMobileStatus/1.1";
            req.Timeout = 20000;
            req.ContentType = type;
            req.Headers["Filename"] = type.Contains("png") ? "cover.png" : "cover.jpg";
            req.Headers["Priority"] = "1";
            req.ContentLength = img.Length;
            using (Stream s = req.GetRequestStream()) s.Write(img, 0, img.Length);
            using (WebResponse resp = req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                Dictionary<string, object> d = U.Json.DeserializeObject(sr.ReadToEnd()) as Dictionary<string, object>;
                string u = U.Str(U.Get(d, "attachment") as Dictionary<string, object>, "url");
                return u != null && u.StartsWith("https://") ? u : "";
            }
        }

        // ------------------------------------------------ ntfy
        void ListenLoop()
        {
            int backoff = 2;
            int myGen = -1;
            string lastId = null;
            while (!stopping)
            {
                Config c = Cfg;
                if (myGen != gen) { myGen = gen; lastId = null; lock (trackLock) { track = null; game = null; } wake.Set(); }
                if (!c.PhoneReady())
                {
                    bool had;
                    lock (trackLock) { had = track != null || game != null; track = null; game = null; }
                    if (had) wake.Set();
                    bool phoneOff = !c.UsePhone;
                    Status(() => phoneOff ? L.T("Đang tắt", "Off") : L.T("Chưa nhập mã điện thoại", "No phone code yet"), null, null);
                    Thread.Sleep(1000);
                    continue;
                }
                string url = c.BaseUrl() + "/" + Uri.EscapeDataString(c.Channel) + "/json";
                try
                {
                    if (lastId == null)
                    {
                        // bật app lúc điện thoại đang phát / đang chơi -> đọc lại tin 30 phút gần nhất (tin sau đè tin trước)
                        string body = U.HttpGet(url + "?poll=1&since=30m", 20000);
                        string[] lines = body.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        lastId = "";
                        replaying = true;
                        try
                        {
                            foreach (string ln in lines)
                            {
                                string id0 = Handle(ln, myGen);
                                if (id0 != null) lastId = id0;
                            }
                        }
                        finally { replaying = false; }
                        Track last;
                        lock (trackLock) last = track;
                        if (last != null && last.Cover.Length == 0) { last.FindCover(); wake.Set(); }
                    }
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url + (lastId.Length > 0 ? "?since=" + lastId : ""));
                    req.UserAgent = "DiscordMobileStatus/1.1";
                    req.Timeout = 30000;
                    req.ReadWriteTimeout = 100000;   // ntfy gửi keepalive mỗi ~45 giây
                    currentReq = req;
                    if (myGen != gen) continue;
                    using (WebResponse resp = req.GetResponse())
                    using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        Status(() => L.T("Đã kết nối, chờ điện thoại phát nhạc", "Connected, waiting for the phone"), null, null);
                        Log.W("Đang nghe kênh " + c.Channel);
                        backoff = 2;
                        string line;
                        while (!stopping && myGen == gen && (line = sr.ReadLine()) != null)
                        {
                            if (line.Trim().Length == 0) continue;
                            string id = Handle(line, myGen);
                            if (id != null) lastId = id;
                        }
                    }
                }
                catch (Exception e)
                {
                    if (stopping) return;
                    if (myGen != gen) continue;
                    int bo = backoff;
                    Status(() => L.T("Mất kết nối, thử lại sau " + bo + " giây", "Disconnected, retrying in " + bo + " s"), null, null);
                    Log.W("ntfy: " + e.Message);
                    Thread.Sleep(backoff * 1000);
                    backoff = Math.Min(backoff * 2, 60);
                }
                finally { currentReq = null; }
            }
        }

        /// <summary>Xử lý một dòng JSON của ntfy. Trả id tin (nếu là tin), để nối lại không mất tin.</summary>
        string Handle(string line, int myGen)
        {
            Dictionary<string, object> ev;
            try { ev = U.Json.DeserializeObject(line) as Dictionary<string, object>; }
            catch (Exception) { return null; }
            if (ev == null || U.Str(ev, "event") != "message") return null;
            string id = U.Str(ev, "id");
            string att = U.Str(U.Get(ev, "attachment") as Dictionary<string, object>, "url");
            long evTime = U.ToLong(U.Get(ev, "time"), 0, long.MaxValue / 1000);
            bool stop;
            // ---- tin game (v2)
            Dictionary<string, object> body2 = null;
            try { body2 = U.Json.DeserializeObject(U.Str(ev, "message") ?? "") as Dictionary<string, object>; } catch (Exception) { }
            if (body2 != null && U.ToLong(U.Get(body2, "v"), 0, 99) == 2 && U.Str(body2, "k") == "game")
            {
                GameTrack g;
                try { g = GameTrack.Parse(body2, att, evTime, out stop); }
                catch (FormatException) { return id; }
                if (myGen != gen) return id;
                lock (trackLock)
                {
                    // nhắc lại cùng game: giữ icon cũ nếu tin nhắc không kèm
                    if (g != null && game != null && g.Pkg == game.Pkg && g.Icon.Length == 0) g.Icon = game.Icon;
                    game = g;
                }
                Log.W("Điện thoại (game): " + (g == null ? "(thoát)" : g.Name));
                wake.Set();
                return id;
            }
            Track t;
            try { t = Track.Parse(U.Str(ev, "message") ?? "", U.ToLong(U.Get(ev, "time"), 0, long.MaxValue / 1000), att, out stop); }
            catch (FormatException) { return id; }   // tin lạ trên kênh -> bỏ qua
            if (t != null && !replaying) t.FindCover();
            if (myGen != gen) return id;
            lock (trackLock) track = t;
            Log.W("Điện thoại: " + (t == null ? "(dừng)" : t.Label() + (t.Playing ? "" : " [tạm dừng]")));
            string at = DateTime.Now.ToString("HH:mm:ss");
            bool none = t == null;
            Status(() => none ? L.T("Đã kết nối · điện thoại không phát", "Connected · phone not playing")
                              : L.T("Đã kết nối · nhận lúc ", "Connected · received at ") + at, null, null);
            wake.Set();
            return id;
        }

        // ------------------------------------------------ Discord
        void WorkLoop()
        {
            while (!stopping)
            {
                wake.WaitOne(15000);
                if (stopping) return;
                lock (rpcLock)
                {
                    try { ApplyGame(); }
                    catch (RpcException e)
                    {
                        if (grpc != null) grpc.Close();
                        gShownSig = null;
                        string gm = e.Message;
                        GameStatus(() => gm + L.T(" — thử lại sau 15 giây", " — retrying in 15 s"));
                    }
                    catch (Exception e)
                    {
                        Log.W("Lỗi (game): " + e);
                        if (grpc != null) grpc.Close();
                        gShownSig = null;
                    }
                    try { Apply(); }
                    catch (RpcException e)
                    {
                        if (rpc != null) rpc.Close();
                        shownSig = null;
                        bool want;
                        want = Pick() != null;
                        string msg = e.Message;
                        bool w = want || U.NowMs() < demoUntil;
                        Status(null, () => w ? msg + L.T(" — thử lại sau 15 giây", " — retrying in 15 s") : L.T("Chưa cần (không phát gì)", "Idle (nothing playing)"), null);
                    }
                    catch (Exception e)
                    {
                        Log.W("Lỗi: " + e);
                        if (rpc != null) rpc.Close();
                        shownSig = null;
                    }
                }
            }
        }

        void Apply()
        {
            Config c = Cfg;
            if (forceReapply) { forceReapply = false; shownSig = null; }
            Track tr = Pick();
            Track raw = tr == null ? Pick(false) : null;      // có bài nhưng loại đó đang tắt chia sẻ?
            string offApp = raw == null ? null : (raw.App == "youtube" ? "YouTube" : "YouTube Music");
            long now = U.NowMs();
            if (now < demoUntil) tr = Track.Demo();
            else if (c.Hidden) tr = null;
            bool show = tr != null && (now < demoUntil || !tr.Expired(c.ShowPaused, now));
            string cid = show ? c.ClientIdFor(tr) : (rpc != null ? rpc.ClientId : null);
            string sig = show ? tr.Signature() : null;

            if (sig == shownSig && rpc != null && rpc.Ok)
            {
                // đặt lại mỗi 60 giây để phát hiện Discord vừa khởi động lại
                if (!show || (DateTime.UtcNow - lastSet).TotalSeconds < 60) return;
            }

            if (rpc == null || !rpc.Ok || rpc.ClientId != cid)
            {
                if (rpc != null)
                {
                    try { if (rpc.Ok) rpc.SetActivity(null); } catch (Exception) { }
                    rpc.Close();
                    rpc = null;
                }
                if (!show)
                {
                    shownSig = null;
                    bool hid = c.Hidden;
                    Status(null, () => L.T("Chờ", "Waiting"), () => IdleText(hid, offApp));
                    return;
                }
                if (string.IsNullOrEmpty(cid)) throw new RpcException(L.T("Chưa nhập Client ID", "No Client ID entered"));
                rpc = new DiscordIpc(cid);
                rpc.Connect();
                Log.W("Đã nối Discord của " + rpc.User);
            }

            if (show)
            {
                SetWithFallback(tr);
                if (sig != shownSig) Log.W("Trạng thái Discord: " + tr.Label());
                lastSet = DateTime.UtcNow;
                string user = rpc.User;
                Track shown = tr;
                Status(null, () => L.T("Đã nối (", "Connected (") + user + ")",
                       () => (shown.App == "youtube" ? L.T("Đang xem: ", "Watching: ") : L.T("Đang nghe: ", "Listening: ")) + shown.Label());
            }
            else
            {
                rpc.SetActivity(null);
                Log.W("Trạng thái Discord: đã ẩn");
                string user2 = rpc.User;
                bool hid2 = c.Hidden;
                Status(null, () => L.T("Đã nối (", "Connected (") + user2 + ")", () => IdleText(hid2, offApp));
            }
            shownSig = sig;
        }

        void GameStatus(Func<string> f)
        {
            stGame = f;
            EmitGame(false);
        }

        void EmitGame(bool force)
        {
            Action<string> h = GameStatusChanged;
            if (h == null) return;
            string v = stGame();
            if (!force && v == lastGameEmit) return;
            lastGameEmit = v;
            h(v);
        }

        /// <summary>Game trên điện thoại -> dòng "Đang chơi" (kết nối Discord riêng, chạy song song với bài nhạc).</summary>
        void ApplyGame()
        {
            Config c = Cfg;
            if (forceReapply) gShownSig = null;
            GameTrack g = PickGame();
            bool own = false;
            string cid = g == null ? null : c.GameClientIdFor(g, out own);
            bool show = g != null && !string.IsNullOrEmpty(cid);
            // chưa có ID game riêng / chung mà trùng ID nhạc thì một kết nối không hiện được 2 thứ -> báo
            string sig = show ? cid + "|" + g.Signature() : null;

            if (sig == gShownSig && grpc != null && grpc.Ok)
            {
                if (!show || (DateTime.UtcNow - gLastSet).TotalSeconds < 60) return;
            }
            if (grpc == null || !grpc.Ok || grpc.ClientId != cid)
            {
                if (grpc != null)
                {
                    try { if (grpc.Ok) grpc.SetActivity(null); } catch (Exception) { }
                    grpc.Close();
                    grpc = null;
                }
                if (!show)
                {
                    gShownSig = null;
                    bool noId = g != null;
                    bool off = !c.ShareGame;
                    bool hidAll = c.Hidden;
                    GameStatus(() => hidAll ? L.HiddenText()
                                   : noId ? L.T("Đang chơi game nhưng chưa nhập Client ID \"Game di động\"", "Playing a game but no \"Mobile game\" Client ID")
                                   : off ? L.T("Đang tắt chia sẻ game", "Game sharing is off") : L.T("Không chơi", "Not playing"));
                    return;
                }
                grpc = new DiscordIpc(cid, Helper.Pid());
                grpc.Connect();
                Log.W("Đã nối Discord (game) ứng dụng " + cid);
            }
            if (show)
            {
                try { grpc.SetActivity(g.Activity(own, gRich)); }
                catch (ActivityRejected)
                {
                    if (!gRich) throw;
                    gRich = false;
                    grpc.SetActivity(g.Activity(own, false));
                }
                if (sig != gShownSig) Log.W("Trạng thái Discord (game): " + g.Name);
                gLastSet = DateTime.UtcNow;
                GameTrack sh = g;
                GameStatus(() => L.T("Đang chơi: ", "Playing: ") + sh.Name);
            }
            else
            {
                grpc.SetActivity(null);
                Log.W("Trạng thái Discord (game): đã ẩn");
                bool hidAll2 = c.Hidden;
                GameStatus(() => hidAll2 ? L.HiddenText() : L.T("Không chơi", "Not playing"));
            }
            gShownSig = sig;
        }

        static string IdleText(bool hidden, string offApp)
        {
            if (hidden) return L.HiddenText();
            if (offApp != null) return L.T("Đang tắt chia sẻ " + offApp, offApp + " sharing is off");
            return L.T("Không có gì", "Nothing");
        }

        /// <summary>Discord bản cũ từ chối trường mới -> lùi dần: bỏ trường mới, rồi đổi sang "Đang chơi".</summary>
        void SetWithFallback(Track tr)
        {
            string[] all = { "full", "plain", "playing" };
            int from = fallback == null ? 0 : Array.IndexOf(all, fallback);
            if (from < 0) from = 0;
            string last = null;
            for (int i = from; i < all.Length; i++)
            {
                try
                {
                    rpc.SetActivity(tr.Activity(all[i]));
                    if (all[i] != (fallback ?? "full")) Log.W("Discord chỉ nhận dạng '" + all[i] + "' (" + last + ")");
                    fallback = all[i];
                    return;
                }
                catch (ActivityRejected e) { last = e.Message; }
            }
            throw new RpcException(L.T("Discord từ chối trạng thái: ", "Discord rejected the status: ") + last);
        }
    }

    // ================================================================ giao diện kiểu Once UI (tự vẽ, không thư viện)
    // Bảng màu tối trung tính + 1 màu nhấn, bo góc lớn, khối (card) có viền mảnh, công tắc trượt mượt.
    static class Ui
    {
        public static readonly Color Page = Color.FromArgb(14, 15, 18);
        public static readonly Color Card = Color.FromArgb(23, 25, 29);
        public static readonly Color CardHi = Color.FromArgb(30, 32, 37);
        public static readonly Color Line = Color.FromArgb(42, 45, 52);
        public static readonly Color Text = Color.FromArgb(237, 238, 240);
        public static readonly Color Muted = Color.FromArgb(146, 151, 160);
        public static readonly Color Faint = Color.FromArgb(98, 103, 112);
        public static readonly Color Brand = Color.FromArgb(88, 101, 242);     // tím Discord
        public static readonly Color Green = Color.FromArgb(59, 214, 113);
        public static readonly Color Red = Color.FromArgb(242, 63, 67);
        public static readonly Color Track = Color.FromArgb(58, 61, 69);

        static Font f(float sz, FontStyle st) { return new Font("Segoe UI", sz, st, GraphicsUnit.Point); }
        public static readonly Font Title = new Font("Segoe UI Semibold", 15f);
        public static readonly Font H = new Font("Segoe UI Semibold", 10f);
        public static readonly Font Body = f(9.5f, FontStyle.Regular);
        public static readonly Font Small = f(8.5f, FontStyle.Regular);
        public static readonly Font Mono = new Font("Consolas", 11f, FontStyle.Bold);

        public static GraphicsPath Round(RectangleF r, float rad)
        {
            GraphicsPath p = new GraphicsPath();
            float d = Math.Min(rad * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>Tỉ lệ màn hình (100 % = 1, 150 % = 1.5). Mọi kích thước giao diện nhân qua D().</summary>
        public static readonly float S = ReadScale();
        static float ReadScale()
        {
            try { using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return Math.Max(1f, g.DpiX / 96f); }
            catch (Exception) { return 1f; }
        }
        public static int D(float v) { return (int)Math.Round(v * S); }

        public static float Ease(float t) { t = Math.Max(0, Math.Min(1, t)); return 1 - (1 - t) * (1 - t) * (1 - t); }   // easeOutCubic

        /// <summary>Đồng hồ hoạt hình dùng chung: chỉ chạy khi có thứ đang chuyển động, xong tự dừng (0 % CPU khi đứng yên).</summary>
        static readonly List<Func<bool>> anims = new List<Func<bool>>();
        static System.Windows.Forms.Timer timer;

        public static void Animate(Func<bool> stepReturnsTrueWhenDone)
        {
            anims.Add(stepReturnsTrueWhenDone);
            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 15;
                timer.Tick += delegate
                {
                    for (int i = anims.Count - 1; i >= 0; i--)
                    {
                        bool done;
                        try { done = anims[i](); } catch (Exception) { done = true; }
                        if (done) anims.RemoveAt(i);
                    }
                    if (anims.Count == 0) timer.Stop();
                };
            }
            if (!timer.Enabled) timer.Start();
        }
    }

    /// <summary>Control vẽ tay, nền đôi (không nháy).</summary>
    class Smooth : Control
    {
        public Smooth()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            ForeColor = Ui.Text;
        }
        protected static void Hq(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        }
    }

    /// <summary>Công tắc trượt kiểu Once UI: rãnh bo tròn, núm trượt có hoạt hình, màu nhấn khi bật.</summary>
    class UiSwitch : Smooth
    {
        bool on;
        float pos;          // 0..1, vị trí núm (hoạt hình)
        bool hover;
        int gen;
        public event EventHandler Toggled;

        public UiSwitch()
        {
            Size = new Size(Ui.D(40), Ui.D(22));
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = Ui.Card;
            SetStyle(ControlStyles.StandardDoubleClick, false);     // bấm nhanh 2 lần = bật/tắt 2 lần
        }

        public bool Checked
        {
            get { return on; }
            set { SetOn(value, false); }
        }

        void SetOn(bool v, bool user)
        {
            if (on == v) return;
            on = v;
            int my = ++gen;
            if (!IsHandleCreated || !Visible) { pos = on ? 1 : 0; Invalidate(); }
            else
            {
                float from = pos;                    // chạy tiếp từ vị trí hiện tại (bấm lại giữa chừng vẫn mượt)
                float target = on ? 1 : 0;
                DateTime t0 = DateTime.UtcNow;
                Ui.Animate(delegate
                {
                    if (my != gen) return true;
                    float k = (float)(DateTime.UtcNow - t0).TotalMilliseconds / 160f;
                    pos = from + (target - from) * Ui.Ease(k);
                    Invalidate();
                    if (k >= 1) { pos = target; Invalidate(); return true; }
                    return false;
                });
            }
            if (user && Toggled != null) Toggled(this, EventArgs.Empty);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { if (Enabled) { Focus(); SetOn(!on, true); } base.OnClick(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter)) { SetOn(!on, true); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Hq(g);
            RectangleF r = new RectangleF(1, 1, Width - 2, Height - 2);
            Color off = hover ? Ui.Mix(Ui.Track, Color.White, 0.08f) : Ui.Track;
            Color onc = hover ? Ui.Mix(Ui.Brand, Color.White, 0.10f) : Ui.Brand;
            Color tc = Ui.Mix(off, onc, pos);
            if (!Enabled) tc = Ui.Mix(tc, Ui.Card, 0.55f);
            using (GraphicsPath p = Ui.Round(r, r.Height / 2))
            using (SolidBrush b = new SolidBrush(tc)) g.FillPath(b, p);
            if (Focused)
                using (GraphicsPath p = Ui.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Height / 2f))
                using (Pen pen = new Pen(Color.FromArgb(150, Ui.Brand), 1f)) g.DrawPath(pen, p);
            float d = r.Height - 6;
            float x = r.X + 3 + (r.Width - 6 - d) * pos;
            using (SolidBrush kb = new SolidBrush(Enabled ? Color.White : Color.FromArgb(150, 150, 150)))
                g.FillEllipse(kb, x, r.Y + 3, d, d);
        }
    }

    /// <summary>Một hàng: tiêu đề + mô tả nhỏ bên trái, công tắc bên phải. Bấm cả hàng cũng bật/tắt.</summary>
    class SwitchRow : Smooth
    {
        public readonly UiSwitch Sw = new UiSwitch();
        string title = "", desc = "";
        public event EventHandler Toggled;

        public SwitchRow()
        {
            Height = Ui.D(48);
            Cursor = Cursors.Hand;
            BackColor = Ui.Card;
            Controls.Add(Sw);
            Sw.Toggled += delegate { if (Toggled != null) Toggled(this, EventArgs.Empty); };
        }

        public bool Checked { get { return Sw.Checked; } set { Sw.Checked = value; } }

        public void SetText(string t, string d)
        {
            title = t; desc = d ?? "";
            Height = desc.Length > 0 ? Ui.D(52) : Ui.D(40);
            Invalidate();
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            Sw.Location = new Point(Width - Sw.Width - 2, (Height - Sw.Height) / 2);
            base.OnLayout(e);
        }

        protected override void OnClick(EventArgs e) { if (Sw.Enabled) Sw.Focus(); base.OnClick(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && Sw.Enabled && ClientRectangle.Contains(e.Location))
            {
                Sw.Checked = !Sw.Checked;
                if (Toggled != null) Toggled(this, EventArgs.Empty);
            }
            base.OnMouseUp(e);
        }
        protected override void OnEnabledChanged(EventArgs e) { Sw.Enabled = Enabled; Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Hq(g);
            int w = Width - Sw.Width - Ui.D(14);
            Color tc = Enabled ? Ui.Text : Ui.Faint;
            if (desc.Length > 0)
            {
                TextRenderer.DrawText(g, title, Ui.Body, new Rectangle(0, Ui.D(7), w, Ui.D(20)), tc, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, desc, Ui.Small, new Rectangle(0, Ui.D(28), w, Ui.D(18)), Ui.Muted, TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
            }
            else
                TextRenderer.DrawText(g, title, Ui.Body, new Rectangle(0, 0, w, Height), tc, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Dòng trạng thái: chấm màu + nhãn + giá trị.</summary>
    class StatusRow : Smooth
    {
        string key = "", val = "—";
        Color dot = Ui.Faint;
        public StatusRow() { Height = Ui.D(24); BackColor = Ui.Card; }
        public void SetKey(string k) { key = k; Invalidate(); }
        public void SetValue(string v, Color c)
        {
            if (v == val && c == dot) return;
            val = v ?? ""; dot = c;
            Invalidate();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Hq(g);
            float dd = Ui.D(7);
            using (SolidBrush b = new SolidBrush(dot)) g.FillEllipse(b, 1, Height / 2f - dd / 2, dd, dd);
            TextRenderer.DrawText(g, key, Ui.Small, new Rectangle(Ui.D(16), 0, Ui.D(78), Height), Ui.Muted, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            TextRenderer.DrawText(g, val, Ui.Body, new Rectangle(Ui.D(98), 0, Width - Ui.D(98), Height), Ui.Text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Khối (card): nền tối hơn trang, viền mảnh, bo góc 14 — xếp các control con theo chiều dọc.</summary>
    class Card : Smooth
    {
        public string Caption = "";

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Hq(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (GraphicsPath p = Ui.Round(r, Ui.D(14)))
            {
                using (SolidBrush b = new SolidBrush(Ui.Card)) g.FillPath(b, p);
                using (Pen pen = new Pen(Ui.Line, 1f)) g.DrawPath(pen, p);
            }
            if (Caption.Length > 0)
                TextRenderer.DrawText(g, Caption.ToUpperInvariant(), Ui.Small, new Point(Ui.D(16), Ui.D(14)), Ui.Muted, TextFormatFlags.NoPadding);
        }
    }

    /// <summary>
    /// Phần thu gọn được: đặt các control con xếp dọc; Expand/Collapse đổi chiều cao có hoạt hình.
    /// Thu gọn hết thì ẩn hẳn (control con không vẽ, không tốn gì).
    /// </summary>
    class Collapse : Panel
    {
        bool open = true;
        int full, gen;
        public bool Animating;
        public event EventHandler HeightChanging;
        public Collapse()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Ui.Card;        // nền đặc = màu khối (vẽ nhanh hơn nền trong suốt)
        }
        public bool Open { get { return open; } }

        /// <summary>Chiều cao khi mở = đáy control con cuối cùng.</summary>
        public int Full()
        {
            int h = 0;
            foreach (Control c in Controls) h = Math.Max(h, c.Bottom);
            return h;
        }

        public void SetOpen(bool v, bool animate)
        {
            open = v;
            full = Full();
            int target = v ? full : 0;
            int my = ++gen;
            if (v) Visible = true;
            Form fm = FindForm();
            if (!animate || !IsHandleCreated || fm == null || !fm.Visible)
            {
                Animating = false;
                Height = target;
                if (!v) Visible = false;
                if (HeightChanging != null) HeightChanging(this, EventArgs.Empty);
                return;
            }
            int from = Height;
            DateTime t0 = DateTime.UtcNow;
            Animating = true;
            Ui.Animate(delegate
            {
                if (my != gen) return true;          // có lần bật/tắt mới hơn -> bỏ hoạt hình cũ
                float k = (float)(DateTime.UtcNow - t0).TotalMilliseconds / 200f;
                Height = from + (int)Math.Round((target - from) * Ui.Ease(k));
                if (HeightChanging != null) HeightChanging(this, EventArgs.Empty);
                if (k >= 1)
                {
                    Animating = false;
                    Height = target;
                    if (!open) Visible = false;
                    if (HeightChanging != null) HeightChanging(this, EventArgs.Empty);
                    return true;
                }
                return false;
            });
        }
    }

    /// <summary>Nút bo tròn: Primary (màu nhấn) hoặc Secondary (xám). Chữ căn giữa tuyệt đối.</summary>
    class UiButton : Smooth
    {
        public bool Primary;
        bool hover, down;
        public UiButton() { Height = Ui.D(36); Cursor = Cursors.Hand; TabStop = true; }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) { OnClick(EventArgs.Empty); e.Handled = true; }
            base.OnKeyDown(e);
        }
        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Hq(g);
            Color bg = Primary ? Ui.Brand : Ui.CardHi;
            if (hover) bg = Ui.Mix(bg, Color.White, Primary ? 0.10f : 0.06f);
            if (down) bg = Ui.Mix(bg, Color.Black, 0.12f);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (GraphicsPath p = Ui.Round(r, Ui.D(10)))
            {
                using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, p);
                if (!Primary) using (Pen pen = new Pen(Ui.Line)) g.DrawPath(pen, p);
                if (Focused) using (Pen pen = new Pen(Color.FromArgb(160, Ui.Brand), 1.5f)) g.DrawPath(pen, p);
            }
            TextRenderer.DrawText(g, Text, Ui.Body, ClientRectangle, Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Chọn ngôn ngữ dạng 2 nút dính nhau (segmented), viên chọn trượt.</summary>
    class Segmented : Smooth
    {
        string[] items = new string[0];
        int index, gen;
        float pos;
        public event EventHandler Changed;
        public Segmented() { Height = Ui.D(28); Cursor = Cursors.Hand; }
        public void SetItems(string[] it) { items = it; Invalidate(); }
        public int Index
        {
            get { return index; }
            set { index = value; pos = value; Invalidate(); }
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || items.Length == 0) { base.OnMouseUp(e); return; }
            int i = Math.Max(0, Math.Min(items.Length - 1, e.X * items.Length / Math.Max(1, Width)));
            if (i == index) { base.OnMouseUp(e); return; }
            float from = pos;
            index = i;
            int my = ++gen;
            DateTime t0 = DateTime.UtcNow;
            Ui.Animate(delegate
            {
                if (my != gen) return true;
                float k = (float)(DateTime.UtcNow - t0).TotalMilliseconds / 180f;
                pos = from + (index - from) * Ui.Ease(k);
                Invalidate();
                return k >= 1;
            });
            if (Changed != null) Changed(this, EventArgs.Empty);
            base.OnMouseUp(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Hq(g);
            RectangleF r = new RectangleF(0.5f, 0.5f, Width - 1, Height - 1);
            using (GraphicsPath p = Ui.Round(r, r.Height / 2))
            {
                using (SolidBrush b = new SolidBrush(Ui.Card)) g.FillPath(b, p);
                using (Pen pen = new Pen(Ui.Line)) g.DrawPath(pen, p);
            }
            if (items.Length == 0) return;
            float w = (Width - 4f) / items.Length;
            using (GraphicsPath p = Ui.Round(new RectangleF(2 + pos * w, 2, w, Height - 4), (Height - 4) / 2f))
            using (SolidBrush b = new SolidBrush(Ui.Track)) g.FillPath(b, p);
            for (int i = 0; i < items.Length; i++)
                TextRenderer.DrawText(g, items[i], Ui.Small, new Rectangle((int)(2 + i * w), 0, (int)w, Height),
                    i == index ? Ui.Text : Ui.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Ô nhập mã: TextBox nền tối đặt trong khung bo tròn.</summary>
    class UiInput : Smooth
    {
        public readonly TextBox Box = new TextBox();
        public UiInput()
        {
            Height = Ui.D(38);
            BackColor = Ui.Card;
            Box.BorderStyle = BorderStyle.None;
            Box.BackColor = Ui.Page;
            Box.ForeColor = Ui.Text;
            Box.Font = Ui.Mono;
            Controls.Add(Box);
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
        }
        protected override void OnLayout(LayoutEventArgs e)
        {
            Box.Location = new Point(Ui.D(12), (Height - Box.Height) / 2);
            Box.Width = Width - Ui.D(24);
            base.OnLayout(e);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            Hq(g);
            using (GraphicsPath p = Ui.Round(new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), Ui.D(10)))
            {
                using (SolidBrush b = new SolidBrush(Ui.Page)) g.FillPath(b, p);
                using (Pen pen = new Pen(Box.Focused ? Ui.Brand : Ui.Line, Box.Focused ? 1.5f : 1f)) g.DrawPath(pen, p);
            }
        }
    }

    /// <summary>Menu chuột phải ở khay theo tông tối.</summary>
    class DarkMenu : ToolStripProfessionalRenderer
    {
        public DarkMenu() : base(new DarkColors()) { RoundedEdges = true; }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor = e.Item.Enabled ? Ui.Text : Ui.Faint; base.OnRenderItemText(e); }
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(e.ImageRectangle.X, e.ImageRectangle.Y + 1, e.ImageRectangle.Width, e.ImageRectangle.Height - 2);
            using (Pen p = new Pen(Ui.Brand, 2f))
                g.DrawLines(p, new PointF[] { new PointF(r.X + 3, r.Y + r.Height / 2f), new PointF(r.X + r.Width / 2.6f, r.Bottom - 4), new PointF(r.Right - 3, r.Y + 3) });
        }
        class DarkColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return Ui.Card; } }
            public override Color ImageMarginGradientBegin { get { return Ui.Card; } }
            public override Color ImageMarginGradientMiddle { get { return Ui.Card; } }
            public override Color ImageMarginGradientEnd { get { return Ui.Card; } }
            public override Color MenuBorder { get { return Ui.Line; } }
            public override Color MenuItemBorder { get { return Ui.CardHi; } }
            public override Color MenuItemSelected { get { return Ui.CardHi; } }
            public override Color MenuItemSelectedGradientBegin { get { return Ui.CardHi; } }
            public override Color MenuItemSelectedGradientEnd { get { return Ui.CardHi; } }
            public override Color SeparatorDark { get { return Ui.Line; } }
            public override Color SeparatorLight { get { return Ui.Card; } }
            public override Color CheckBackground { get { return Ui.Card; } }
            public override Color CheckSelectedBackground { get { return Ui.CardHi; } }
            public override Color CheckPressedBackground { get { return Ui.CardHi; } }
        }
    }

    /// <summary>
    /// Thanh cuộn mảnh kiểu Once UI: chỉ là một viên bo tròn nhỏ màu tối trên nền đen,
    /// to ra một chút khi rê chuột, kéo được, bấm vào rãnh để nhảy trang. Nội dung vừa khung thì ẩn hẳn.
    /// </summary>
    class ThinBar : Smooth
    {
        public Scroller Owner;
        bool hover, drag;
        int dragOff;

        public ThinBar() { Cursor = Cursors.Default; BackColor = Ui.Page; }

        Rectangle Thumb()
        {
            int view = Owner.Height, content = Owner.ContentHeight;
            if (content <= view || view <= 0) return Rectangle.Empty;
            int pad = Ui.D(4);
            int track = Height - 2 * pad;
            int th = Math.Max(Ui.D(32), track * view / content);
            int range = content - view;
            int ty = pad + (int)((long)(track - th) * Owner.Value / Math.Max(1, range));
            return new Rectangle(0, ty, Width, th);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Rectangle t = Thumb();
            if (e.Button == MouseButtons.Left && !t.IsEmpty)
            {
                if (e.Y >= t.Top && e.Y < t.Bottom) { drag = true; dragOff = e.Y - t.Top; Capture = true; }
                else Owner.ScrollBy((e.Y < t.Top ? -1 : 1) * Owner.Height * 9 / 10, true);     // bấm rãnh = nhảy gần 1 trang
                Invalidate();
            }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (drag)
            {
                Rectangle t = Thumb();
                int pad = Ui.D(4);
                int track = Height - 2 * pad - t.Height;
                if (track > 0) Owner.ScrollTo((int)((long)(e.Y - dragOff - pad) * (Owner.ContentHeight - Owner.Height) / track), false);
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseUp(MouseEventArgs e) { drag = false; Capture = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Rectangle t = Thumb();
            if (t.IsEmpty) return;
            Graphics g = e.Graphics;
            Hq(g);
            float w = (hover || drag) ? Ui.D(6) : Ui.D(4);
            float x = (Width - w) / 2f;
            Color c = drag ? Color.FromArgb(92, 97, 108) : hover ? Color.FromArgb(70, 74, 84) : Color.FromArgb(46, 49, 56);
            using (GraphicsPath p = Ui.Round(new RectangleF(x, t.Y, w, t.Height), w / 2))
            using (SolidBrush b = new SolidBrush(c)) g.FillPath(b, p);
        }
    }

    /// <summary>
    /// Khung cuộn tự làm (thay cho thanh cuộn Windows to thô): nội dung đặt trong Content, cuộn bằng cách
    /// dời Content lên; lăn chuột có trượt mượt; thanh cuộn mảnh nằm trong rãnh hẹp bên phải.
    /// </summary>
    class Scroller : Panel
    {
        public readonly Panel Content = new Panel();
        public readonly ThinBar Bar = new ThinBar();
        public static readonly int Gutter = Ui.D(10);
        int value, contentH, target, gen;

        public Scroller()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
            BackColor = Ui.Page;
            Content.BackColor = Ui.Page;
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(Content, true, null);
            Bar.Owner = this;
            Content.ControlAdded += delegate (object s, ControlEventArgs e) { Hook(e.Control); };
            Controls.Add(Content);
            Controls.Add(Bar);
        }

        void Hook(Control c)
        {
            if (c.TabStop || c is TextBox) c.Enter += delegate { Reveal(c); };
            foreach (Control k in c.Controls) Hook(k);
            c.ControlAdded += delegate (object s, ControlEventArgs e) { Hook(e.Control); };
        }

        public int Value { get { return value; } }
        public int ContentHeight { get { return contentH; } }
        int Max { get { return Math.Max(0, contentH - Height); } }

        public void SetContentHeight(int h)
        {
            contentH = h;
            value = Math.Min(value, Max);
            target = Math.Min(target, Max);
            Place();
        }

        void Place()
        {
            Content.SetBounds(0, -value, Width - Gutter, Math.Max(contentH, Height));
            Bar.SetBounds(Width - Gutter, 0, Gutter, Height);
            Bar.Invalidate();
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); value = Math.Min(value, Max); target = Math.Min(target, Max); Place(); }

        /// <summary>Tab tới một control khuất -> cuộn cho nó hiện ra.</summary>
        public void Reveal(Control c)
        {
            int top = 0;
            for (Control x = c; x != null && x != Content; x = x.Parent) top += x.Top;
            int bottom = top + c.Height;
            if (top < value) ScrollTo(top - Ui.D(12), true);
            else if (bottom > value + Height) ScrollTo(bottom - Height + Ui.D(12), true);
        }

        public void ScrollBy(int delta, bool animate) { ScrollTo(target + delta, animate); }

        public void ScrollTo(int v, bool animate)
        {
            target = Math.Max(0, Math.Min(Max, v));
            int my = ++gen;
            if (!animate || !Visible)
            {
                value = target;
                Place();
                return;
            }
            Ui.Animate(delegate
            {
                if (my != gen) return true;
                int diff = target - value;
                if (Math.Abs(diff) <= 1) { value = target; Place(); return true; }
                value += (int)Math.Round(diff * 0.3);                    // trượt chậm dần về đích (~0,15 s)
                Place();
                return false;
            });
        }

        /// <summary>Lăn chuột: delta 120 = một nấc ≈ 3 dòng.</summary>
        public void Wheel(int delta)
        {
            int lines = Math.Max(1, SystemInformation.MouseWheelScrollLines);
            ScrollBy(-delta * lines * Ui.D(18) / 120, true);
        }
    }

    // ================================================================ cửa sổ + khay hệ thống
    class MainForm : Form, IMessageFilter
    {

        /// <summary>Lăn chuột ở bất cứ đâu trong cửa sổ đều cuộn danh sách (mặc định WinForms chỉ cuộn khi panel có focus).</summary>
        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x020A || !Visible || scroller == null) return false;
            Control c = Control.FromHandle(m.HWnd);
            if (c == null || c.FindForm() != this) return false;
            int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
            scroller.Wheel(delta);
            return true;
        }

        readonly EventWaitHandle showEvt;
        bool allowShow;
        bool exiting;
        Engine engine;
        Config cfg;
        bool loading;

        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int val, int size);

        NotifyIcon tray;
        ToolStripMenuItem miOpen, miHide, miMusic, miVideo, miGame, miExit;

        // khung
        Scroller scroller;
        Label lblTitle, lblSub;
        Segmented seg;
        Card cStatus, cShare, cPhone, cApp;
        StatusRow rPhone, rPc, rGame, rDiscord, rNow;
        SwitchRow swShare, swMusic, swVideo, swGame, swPaused, swPc, swPhone, swStartup;
        Collapse colShare, colPhone;
        Control[] shareItems;
        Label lblCode, lblCodeHint, lblPcHint;
        UiInput inCode;
        UiButton btnTest, btnTray, btnSaveCode;

        public MainForm(bool startHidden, EventWaitHandle showEvt)
        {
            this.showEvt = showEvt;
            Config.ChooseLocation();
            cfg = Config.Load();
            if (cfg.ArtTopic.Length == 0)
            {
                cfg.ArtTopic = Config.RandomTopic("yts-art-");
                try { cfg.Save(); } catch (Exception) { }
            }
            if (cfg.Lang != "vi" && cfg.Lang != "en")
                cfg.Lang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "vi" ? "vi" : "en";
            L.En = cfg.Lang == "en";
            AutoStart.Refresh();
            allowShow = !startHidden;
            Log.W("Khởi động Discord Mobile Status 1.1.2, cấu hình: " + Config.FilePath);

            BuildUi();
            ApplyLang();
            LoadToUi();
            Application.AddMessageFilter(this);
            IntPtr h = Handle;          // tạo handle ngay để nhận trạng thái kể cả khi khởi động ẩn
            GC.KeepAlive(h);

            engine = new Engine(cfg.Copy());
            engine.StatusChanged += OnStatus;
            engine.GameStatusChanged += OnGameStatus;
            engine.Start();
            if (!allowShow) ScheduleTrim(15000);       // khởi động ẩn cùng Windows -> trả RAM khởi động thừa

            Thread t = new Thread(delegate ()
            {
                while (true)
                {
                    showEvt.WaitOne();
                    try { BeginInvoke(new Action(ShowFromTray)); } catch (Exception) { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (!allowShow)
            {
                value = false;
                if (!IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            // thanh tiêu đề tối (Windows 10 20H1+ / 11); bản cũ hơn thì bỏ qua
            try { int on = 1; if (DwmSetWindowAttribute(Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref on, 4); } catch (Exception) { }
        }

        // ------------------------------------------------ dựng giao diện
        static readonly int W = Ui.D(460);     // bề rộng nội dung
        static readonly int M = Ui.D(16);      // lề
        static readonly int P = Ui.D(16);      // lề trong khối
        static int D(float v) { return Ui.D(v); }

        void BuildUi()
        {
            SuspendLayout();
            Text = L.App;
            Font = Ui.Body;
            BackColor = Ui.Page;
            ForeColor = Ui.Text;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;    // tự nhân tỉ lệ qua D()
            DoubleBuffered = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            scroller = new Scroller();
            scroller.Dock = DockStyle.Fill;
            Controls.Add(scroller);
            Control.ControlCollection body = scroller.Content.Controls;

            int cw = W - 2 * M;

            lblTitle = new Label();
            lblTitle.Text = L.App;
            lblTitle.Font = Ui.Title;
            lblTitle.ForeColor = Ui.Text;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(M, D(16));
            body.Add(lblTitle);

            seg = new Segmented();
            seg.SetItems(new string[] { "Tiếng Việt", "English" });
            seg.Index = L.En ? 1 : 0;
            seg.Size = new Size(D(150), D(28));
            int titleH = lblTitle.PreferredHeight;
            seg.Location = new Point(W - M - D(150), lblTitle.Top + (titleH - seg.Height) / 2);     // ngang hàng tiêu đề
            seg.Changed += delegate
            {
                bool en = seg.Index == 1;
                if (en == L.En) return;
                L.En = en;
                cfg.Lang = en ? "en" : "vi";
                WriteConfig();
                ApplyLang();
                engine.Cfg = cfg.Copy();
                engine.Relabel();
            };
            body.Add(seg);

            lblSub = new Label();
            lblSub.ForeColor = Ui.Muted;
            lblSub.Font = Ui.Small;
            lblSub.AutoSize = false;
            lblSub.Size = new Size(cw, D(18));
            lblSub.Location = new Point(M + 1, Math.Max(lblTitle.Top + titleH, seg.Bottom) + D(10));   // cách tiêu đề + ô ngôn ngữ
            body.Add(lblSub);

            // ---- khối Trạng thái
            cStatus = new Card();
            cStatus.Width = cw;
            rPhone = Row(cStatus); rPc = Row(cStatus); rGame = Row(cStatus); rDiscord = Row(cStatus); rNow = Row(cStatus);
            StackCard(cStatus, new Control[] { rPhone, rPc, rGame, rDiscord, rNow }, D(2));
            body.Add(cStatus);

            // ---- khối Chia sẻ: công tắc chính + các công tắc con thu gọn được
            cShare = new Card();
            cShare.Width = cw;
            swShare = SRow(cShare);
            colShare = new Collapse();
            colShare.Width = cw - 2 * P;
            swMusic = SRow(colShare); swVideo = SRow(colShare); swGame = SRow(colShare); swPaused = SRow(colShare);
            shareItems = new Control[] { Sep(colShare), swMusic, swVideo, swGame, swPaused };
            Stack(colShare, shareItems, 0);
            cShare.Controls.Add(colShare);
            colShare.Location = new Point(P, 0);
            colShare.HeightChanging += delegate { Relayout(); };
            body.Add(cShare);

            // ---- khối Nguồn: trình duyệt PC + điện thoại (bật mới hiện ô mã)
            cPhone = new Card();
            cPhone.Width = cw;
            swPc = SRow(cPhone);
            lblPcHint = Hint(cPhone);
            swPhone = SRow(cPhone);
            colPhone = new Collapse();
            int iw = cw - 2 * P;
            colPhone.Width = iw;
            lblCode = new Label(); lblCode.ForeColor = Ui.Muted; lblCode.Font = Ui.Small; lblCode.AutoSize = false; lblCode.Size = new Size(iw, D(18));
            inCode = new UiInput(); inCode.Size = new Size(iw - D(92), D(38));
            btnSaveCode = new UiButton(); btnSaveCode.Primary = true; btnSaveCode.Size = new Size(D(84), D(38));
            lblCodeHint = new Label(); lblCodeHint.ForeColor = Ui.Faint; lblCodeHint.Font = Ui.Small; lblCodeHint.AutoSize = false; lblCodeHint.Size = new Size(iw, D(34));
            colPhone.Controls.Add(lblCode); colPhone.Controls.Add(inCode); colPhone.Controls.Add(btnSaveCode); colPhone.Controls.Add(lblCodeHint);
            lblCode.Location = new Point(0, D(6));
            inCode.Location = new Point(0, D(28));
            btnSaveCode.Location = new Point(iw - D(84), D(28));
            lblCodeHint.Location = new Point(0, D(72));
            colPhone.Height = D(108);
            cPhone.Controls.Add(colPhone);
            colPhone.HeightChanging += delegate { Relayout(); };
            body.Add(cPhone);

            // ---- khối Ứng dụng
            cApp = new Card();
            cApp.Width = cw;
            swStartup = SRow(cApp);
            StackCard(cApp, new Control[] { swStartup }, 0);
            body.Add(cApp);

            // ---- nút
            btnTest = new UiButton(); btnTest.Size = new Size((cw - D(10)) / 2, D(38));
            btnTray = new UiButton(); btnTray.Primary = true; btnTray.Size = new Size((cw - D(10)) / 2, D(38));
            body.Add(btnTest);
            body.Add(btnTray);

            // ---- sự kiện
            swShare.Toggled += delegate { colShare.SetOpen(swShare.Checked, true); SaveFlags(); };
            swMusic.Toggled += delegate { SaveFlags(); };
            swVideo.Toggled += delegate { SaveFlags(); };
            swGame.Toggled += delegate { SaveFlags(); };
            swPaused.Toggled += delegate { SaveFlags(); };
            swPc.Toggled += delegate { SaveFlags(); };
            swPhone.Toggled += delegate { colPhone.SetOpen(swPhone.Checked, true); SaveFlags(); };
            swStartup.Toggled += delegate
            {
                if (loading) return;
                try { AutoStart.Set(swStartup.Checked); }
                catch (Exception e)
                {
                    MessageBox.Show(this, L.T("Không ghi được cài đặt khởi động: ", "Could not change the startup setting: ") + e.Message, L.App);
                    loading = true;
                    try { swStartup.Checked = AutoStart.IsOn(); } catch (Exception) { }
                    loading = false;
                }
            };
            btnSaveCode.Click += delegate { SaveCode(true); };
            inCode.Box.KeyDown += delegate (object s, KeyEventArgs e) { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; SaveCode(true); } };
            btnTray.Click += delegate { HideToTray(); };
            btnTest.Click += delegate
            {
                engine.Demo();
                MessageBox.Show(this, L.T("Đã đặt một bài mẫu lên trạng thái Discord trong 20 giây.\nMở hồ sơ Discord của bạn để xem.",
                                          "A test track is on your Discord status for 20 seconds.\nOpen your Discord profile to see it."),
                    L.App, MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // ---- khay
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Renderer = new DarkMenu();
            menu.ShowImageMargin = true;
            miOpen = new ToolStripMenuItem(); miOpen.Font = new Font(menu.Font, FontStyle.Bold);
            miOpen.Click += delegate { ShowFromTray(); };
            miHide = new ToolStripMenuItem();
            miHide.Click += delegate { swShare.Checked = !swShare.Checked; colShare.SetOpen(swShare.Checked, Visible); SaveFlags(); };
            miMusic = new ToolStripMenuItem(); miMusic.Click += delegate { swMusic.Checked = !swMusic.Checked; SaveFlags(); };
            miVideo = new ToolStripMenuItem(); miVideo.Click += delegate { swVideo.Checked = !swVideo.Checked; SaveFlags(); };
            miGame = new ToolStripMenuItem(); miGame.Click += delegate { swGame.Checked = !swGame.Checked; SaveFlags(); };
            miExit = new ToolStripMenuItem(); miExit.Click += delegate { ExitApp(); };
            menu.Items.Add(miOpen);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miHide);
            menu.Items.Add(miMusic);
            menu.Items.Add(miVideo);
            menu.Items.Add(miGame);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            tray = new NotifyIcon();
            try { tray.Icon = new Icon(Icon, SystemInformation.SmallIconSize); }
            catch (Exception) { tray.Icon = SystemIcons.Application; }
            tray.Text = L.App;
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowFromTray(); };

            int maxH = Screen.PrimaryScreen.WorkingArea.Height - D(80);
            ClientSize = new Size(W + Scroller.Gutter, Math.Min(D(800), maxH));
            ResumeLayout(false);
        }

        // ---- helper dựng khối
        StatusRow Row(Control parent) { StatusRow r = new StatusRow(); r.Width = parent.Width - 2 * P; parent.Controls.Add(r); return r; }
        SwitchRow SRow(Control parent)
        {
            SwitchRow r = new SwitchRow();
            r.Width = (parent is Collapse) ? parent.Width : parent.Width - 2 * P;
            parent.Controls.Add(r);
            return r;
        }
        Label Hint(Control parent)
        {
            Label l = new Label();
            l.ForeColor = Ui.Faint; l.Font = Ui.Small; l.AutoSize = false; l.Size = new Size(parent.Width - 2 * P, D(18));
            parent.Controls.Add(l);
            return l;
        }
        Control Sep(Control parent)
        {
            Panel p = new Panel();
            p.Height = 1; p.Width = parent.Width; p.BackColor = Ui.Line;
            parent.Controls.Add(p);
            return p;
        }
        static void Stack(Control parent, Control[] items, int gap)
        {
            int y = 0;
            foreach (Control c in items) { c.Location = new Point(0, y); y = c.Bottom + gap; }
            parent.Height = y;
        }
        static void StackCard(Card card, Control[] items, int gap)
        {
            int y = D(38);
            foreach (Control c in items) { c.Location = new Point(P, y); y = c.Bottom + gap; }
            card.Height = y + D(10);
        }

        /// <summary>Xếp lại các khối theo chiều dọc (gọi khi một phần thu gọn / mở ra).</summary>
        void Relayout()
        {
            scroller.Content.SuspendLayout();
            int gap = D(12);
            // xếp lại các hàng trong khối (chiều cao hàng đổi theo ngôn ngữ)
            int yy = 0;
            foreach (Control c in shareItems) { c.Location = new Point(0, yy); yy = c.Bottom; }
            if (!colShare.Animating) colShare.Height = colShare.Open ? colShare.Full() : 0;
            swStartup.Location = new Point(P, D(38));
            cApp.Height = swStartup.Bottom + D(10);
            int y = lblSub.Bottom + D(14);
            cStatus.Location = new Point(M, y); y += cStatus.Height + gap;

            // khối Chia sẻ (Height = 0 khi thu gọn; không dùng .Visible vì cửa sổ ẩn thì mọi con đều báo false)
            swShare.Location = new Point(P, D(38));
            colShare.Location = new Point(P, swShare.Bottom + D(2));
            cShare.Height = (colShare.Height > 0 ? colShare.Bottom : swShare.Bottom) + D(10);
            cShare.Location = new Point(M, y); y += cShare.Height + gap;

            // khối Nguồn
            swPc.Location = new Point(P, D(38));
            lblPcHint.Location = new Point(P, swPc.Bottom - D(2));
            swPhone.Location = new Point(P, lblPcHint.Bottom + D(6));
            colPhone.Location = new Point(P, swPhone.Bottom);
            cPhone.Height = (colPhone.Height > 0 ? colPhone.Bottom : swPhone.Bottom) + D(10);
            cPhone.Location = new Point(M, y); y += cPhone.Height + gap;

            cApp.Location = new Point(M, y); y += cApp.Height + D(16);

            btnTest.Location = new Point(M, y);
            btnTray.Location = new Point(M + btnTest.Width + D(10), y);
            y += btnTest.Height + M;
            scroller.Content.ResumeLayout(true);
            scroller.SetContentHeight(y);
        }

        /// <summary>Đặt toàn bộ chữ trên cửa sổ + menu khay theo ngôn ngữ đang chọn.</summary>
        void ApplyLang()
        {
            lblSub.Text = L.T("YouTube, YouTube Music và game điện thoại → trạng thái Discord.", "YouTube, YouTube Music and phone games → your Discord status.");
            cStatus.Caption = L.T("Trạng thái", "Status");
            cShare.Caption = L.T("Chia sẻ", "Sharing");
            cPhone.Caption = L.T("Nguồn", "Sources");
            cApp.Caption = L.T("Ứng dụng", "App");
            rPhone.SetKey(L.T("Điện thoại", "Phone"));
            rPc.SetKey(L.T("Máy tính", "Computer"));
            rGame.SetKey(L.T("Game", "Game"));
            rDiscord.SetKey("Discord");
            rNow.SetKey(L.T("Đang hiện", "Showing"));
            swShare.SetText(L.T("Chia sẻ lên Discord", "Share on Discord"), L.T("Tắt để ẩn hết khỏi trạng thái Discord", "Turn off to hide everything from your status"));
            swMusic.SetText("YouTube Music", L.T("Đang nghe…", "Listening to…"));
            swVideo.SetText("YouTube", L.T("Đang xem…", "Watching…"));
            swGame.SetText(L.T("Game trên điện thoại", "Phone games"), L.T("Đang chơi… (cần app trên điện thoại)", "Playing… (needs the phone app)"));
            swPaused.SetText(L.T("Vẫn hiện khi tạm dừng", "Show while paused"), null);
            swPc.SetText(L.T("Trình duyệt trên máy này", "This PC's browser"), L.T("YouTube / YouTube Music trong Chrome, Edge, Cốc Cốc…", "YouTube / YouTube Music in Chrome, Edge, Brave…"));
            lblPcHint.Text = PcMedia.Supported ? "" : L.T("Cần Windows 10 bản 1809 trở lên", "Needs Windows 10 1809 or newer");
            lblPcHint.Height = lblPcHint.Text.Length > 0 ? D(18) : 0;
            swPhone.SetText(L.T("Điện thoại Android", "Android phone"), L.T("Nhạc, video và game trên điện thoại", "Music, videos and games on your phone"));
            lblCode.Text = L.T("Mã trong app Discord Mobile Status trên điện thoại", "Code shown in the Discord Mobile Status phone app");
            btnSaveCode.Text = L.T("Lưu", "Save");
            lblCodeHint.Text = L.T("Dạng nc-xxxx-xxxx-xxxx. Chưa có app? Tải DiscordMobileStatus.apk ở trang Releases.",
                                   "Looks like nc-xxxx-xxxx-xxxx. No app yet? Get DiscordMobileStatus.apk from Releases.");
            swStartup.SetText(L.T("Chạy cùng Windows", "Start with Windows"), L.T("Tự chạy ẩn dưới khay khi bật máy", "Starts hidden in the tray"));
            btnTest.Text = L.T("Thử Discord", "Test Discord");
            btnTray.Text = L.T("Ẩn xuống khay", "Hide to tray");
            miOpen.Text = L.T("Mở ", "Open ") + L.App;
            miHide.Text = L.T("Chia sẻ lên Discord", "Share on Discord");
            miMusic.Text = "YouTube Music";
            miVideo.Text = "YouTube";
            miGame.Text = L.T("Game trên điện thoại", "Phone games");
            miExit.Text = L.T("Thoát", "Exit");
            foreach (Control c in new Control[] { cStatus, cShare, cPhone, cApp }) c.Invalidate();
            if (pending != null || pendingGame != null) PaintStatus();
            Relayout();
        }

        // ------------------------------------------------ dữ liệu <-> giao diện
        void LoadToUi()
        {
            loading = true;
            swShare.Checked = !cfg.Hidden;
            swMusic.Checked = cfg.ShareMusic;
            swVideo.Checked = cfg.ShareVideo;
            swGame.Checked = cfg.ShareGame;
            swPaused.Checked = cfg.ShowPaused;
            swPc.Checked = cfg.UsePc && PcMedia.Supported;
            swPc.Enabled = PcMedia.Supported;
            swPhone.Checked = cfg.UsePhone;
            inCode.Box.Text = cfg.Channel;
            try { swStartup.Checked = AutoStart.IsOn(); } catch (Exception) { }
            colShare.SetOpen(swShare.Checked, false);
            colPhone.SetOpen(swPhone.Checked, false);
            SyncMenu();
            loading = false;
            Relayout();
        }

        void SyncMenu()
        {
            miHide.Checked = !cfg.Hidden;
            miMusic.Checked = cfg.ShareMusic;
            miVideo.Checked = cfg.ShareVideo;
            miGame.Checked = cfg.ShareGame;
            miMusic.Enabled = miVideo.Enabled = miGame.Enabled = !cfg.Hidden;
        }

        /// <summary>Lưu mã điện thoại. Trả false nếu mã sai.</summary>
        bool SaveCode(bool notify)
        {
            string ch = inCode.Box.Text.Trim();
            if (ch.Length > 0 && !Regex.IsMatch(ch, "^[A-Za-z0-9_-]{6,64}$"))
            {
                Warn(L.T("Mã điện thoại chưa đúng (dạng nc-abcd-efgh-jkmn).", "The phone code looks wrong (like nc-abcd-efgh-jkmn)."), inCode.Box);
                return false;
            }
            bool changed = ch != cfg.Channel;
            cfg.Channel = ch;
            if (!WriteConfig()) return false;
            if (changed) engine.Reconfigure(cfg.Copy());
            else { engine.Cfg = cfg.Copy(); engine.Poke(); }
            if (notify) tray.ShowBalloonTip(2000, L.App, L.T("Đã lưu mã.", "Code saved."), ToolTipIcon.Info);
            return true;
        }

        void SaveFlags()
        {
            if (loading) return;
            bool phoneChanged = cfg.UsePhone != swPhone.Checked;
            cfg.Hidden = !swShare.Checked;
            cfg.ShareMusic = swMusic.Checked;
            cfg.ShareVideo = swVideo.Checked;
            cfg.ShareGame = swGame.Checked;
            cfg.ShowPaused = swPaused.Checked;
            if (PcMedia.Supported) cfg.UsePc = swPc.Checked;
            cfg.UsePhone = swPhone.Checked;
            SyncMenu();
            WriteConfig();
            if (phoneChanged) engine.Reconfigure(cfg.Copy());
            else { engine.Cfg = cfg.Copy(); engine.Poke(); }
        }

        bool WriteConfig()
        {
            try { cfg.Save(); return true; }
            catch (Exception e)
            {
                MessageBox.Show(this, L.T("Không lưu được ", "Could not save ") + Config.FilePath + ":\n" + e.Message, L.App, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        void Warn(string msg, Control focus)
        {
            ShowFromTray();
            MessageBox.Show(this, msg, L.App, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            focus.Focus();
        }

        // ------------------------------------------------ trạng thái (cửa sổ ẩn thì không vẽ)
        string pendingGame;
        string[] pending;
        string lastTip;

        void OnGameStatus(string g)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    pendingGame = g;
                    if (Visible) PaintStatus();
                }));
            }
            catch (Exception) { }
        }

        void OnStatus(string phone, string pc, string discord, string now)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    pending = new string[] { phone, pc, discord, now };
                    string tip = "Discord Status · " + now;
                    if (tip.Length > 63) tip = tip.Substring(0, 62) + "…";
                    if (tip != lastTip) { lastTip = tip; tray.Text = tip; }
                    if (Visible) PaintStatus();
                }));
            }
            catch (Exception) { }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible) PaintStatus();
        }

        /// <summary>Màu chấm: xanh = đang chạy / đang hiện, xám = chờ, đỏ = lỗi / tắt.</summary>
        static Color Tone(string s)
        {
            if (string.IsNullOrEmpty(s) || s == "—") return Ui.Faint;
            string l = s.ToLowerInvariant();
            if (l.Contains("lỗi") || l.Contains("error") || l.Contains("mất kết nối") || l.Contains("disconnected") || l.Contains("chưa mở") || l.Contains("not open")
                || l.Contains("từ chối") || l.Contains("rejected")) return Ui.Red;
            if (l.Contains("đang nghe") || l.Contains("đang xem") || l.Contains("đang chơi") || l.Contains("đang phát") || l.Contains("listening")
                || l.Contains("watching") || l.Contains("playing") || l.Contains("đã nối") || l.Contains("connected") || l.Contains("đã kết nối")) return Ui.Green;
            return Ui.Faint;
        }

        void PaintStatus()
        {
            if (pendingGame != null) rGame.SetValue(pendingGame, Tone(pendingGame));
            string[] p = pending;
            if (p == null) return;
            rPhone.SetValue(p[0], Tone(p[0]));
            rPc.SetValue(p[1], Tone(p[1]));
            rDiscord.SetValue(p[2], Tone(p[2]));
            rNow.SetValue(p[3], Tone(p[3]));
        }

        // ------------------------------------------------ khay
        System.Windows.Forms.Timer trimTimer;

        /// <summary>Sau vài giây chạy ẩn thì trả RAM không dùng cho Windows (một lần).</summary>
        void ScheduleTrim(int ms)
        {
            if (trimTimer == null)
            {
                trimTimer = new System.Windows.Forms.Timer();
                trimTimer.Tick += delegate
                {
                    trimTimer.Stop();
                    if (!Visible) Program.TrimMemory();
                };
            }
            trimTimer.Stop();
            trimTimer.Interval = ms;
            trimTimer.Start();
        }

        void ShowFromTray()
        {
            allowShow = true;
            PaintStatus();
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        void HideToTray()
        {
            Hide();
            ScheduleTrim(3000);
            if (cfg.Balloon)
            {
                tray.ShowBalloonTip(4000, L.App + L.T(" vẫn đang chạy", " is still running"),
                    L.T("App chạy ẩn dưới khay hệ thống. Muốn tắt hẳn: chuột phải biểu tượng › Thoát.",
                        "It keeps running in the system tray. To quit: right-click the icon › Exit."), ToolTipIcon.Info);
                cfg.Balloon = false;
                try { cfg.Save(); } catch (Exception) { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;        // bấm X -> xuống khay, không tắt
                HideToTray();
                return;
            }
            exiting = true;
            ShutdownEngine();
            base.OnFormClosing(e);
        }

        void ExitApp()
        {
            exiting = true;
            ShutdownEngine();
            Close();
            Application.Exit();
        }

        bool shutDown;
        void ShutdownEngine()
        {
            if (shutDown) return;
            shutDown = true;
            Log.W("Thoát Discord Mobile Status");
            try { Application.RemoveMessageFilter(this); } catch (Exception) { }
            try { engine.Stop(); } catch (Exception) { }
            tray.Visible = false;
            tray.Dispose();
        }
    }
}
