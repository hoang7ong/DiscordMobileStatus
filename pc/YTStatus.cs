// YT Status — hiện bạn đang nghe / xem gì trên YouTube, YouTube Music (điện thoại) lên trạng thái Discord.
// Một file .exe, không cần cài: bấm X thì xuống khay hệ thống, chuột phải biểu tượng › Thoát để tắt hẳn.
// Biên dịch bằng trình biên dịch có sẵn trong Windows (C# 5): xem BUILD_EXE.bat.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
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

[assembly: System.Reflection.AssemblyTitle("YT Status")]
[assembly: System.Reflection.AssemblyProduct("YT Status")]
[assembly: System.Reflection.AssemblyDescription("Shows what you play on YouTube / YouTube Music as your Discord status")]
[assembly: System.Reflection.AssemblyVersion("1.0.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.1.0")]

namespace YTStatus
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
            bool created;
            using (Mutex mutex = new Mutex(true, "YTStatus_Mutex_v1", out created))
            {
                EventWaitHandle showEvt = new EventWaitHandle(false, EventResetMode.AutoReset, "YTStatus_Show_v1");
                if (!created)
                {
                    try { AllowSetForegroundWindow(-1); } catch (Exception) { }
                    showEvt.Set();          // đã chạy sẵn -> bảo bản kia hiện cửa sổ lên
                    return;
                }
                try { SetProcessDPIAware(); } catch (Exception) { }
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
            req.UserAgent = "YTStatus/1.0";
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
            return T("ĐANG TẮT — bỏ tick \"Tắt hiện lên Discord\"", "OFF — untick \"Don't show on Discord\"");
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
        public string IdMusic = "";
        public string IdVideo = "";
        public bool ShowPaused = false;
        public bool Hidden = false;          // "Tạm ẩn trạng thái"
        public bool Balloon = true;          // lần đầu bấm X thì nhắc "app vẫn chạy dưới khay"
        public bool UsePc = true;            // đọc YouTube đang phát trên trình duyệt của chính máy này
        public bool ShareMusic = true;       // chia sẻ bài YouTube Music lên Discord
        public bool ShareVideo = true;       // chia sẻ video YouTube lên Discord
        public string ArtTopic = "";         // kênh ntfy riêng để tải ảnh bìa từ PC lên (Discord cần link ảnh công khai)
        public string Lang = "";             // "vi" hoặc "en"; trống = theo ngôn ngữ Windows

        public static string FilePath;

        public static void ChooseLocation()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "YTStatus");
            string local = Path.Combine(exeDir, "YTStatus.json");
            string roam = Path.Combine(appData, "YTStatus.json");
            if (File.Exists(local)) { FilePath = local; }
            else if (File.Exists(roam)) { FilePath = roam; }
            else
            {
                // ưu tiên để cạnh .exe (mang đi máy khác được); thư mục không ghi được thì dùng AppData
                try
                {
                    string probe = Path.Combine(exeDir, ".ytstatus_probe");
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
            Log.PathFile = Path.Combine(Path.GetDirectoryName(FilePath), "YTStatus.log");
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
                c.IdMusic = U.Str(d, "client_id_ytmusic") ?? "";
                c.IdVideo = U.Str(d, "client_id_youtube") ?? "";
                c.ShowPaused = U.Bool(d, "hien_khi_tam_dung");
                c.Hidden = U.Bool(d, "tam_an");
                c.Balloon = U.Get(d, "nhac_khay") == null || U.Bool(d, "nhac_khay");
                c.UsePc = U.Get(d, "trinh_duyet_pc") == null || U.Bool(d, "trinh_duyet_pc");
                c.ShareMusic = U.Get(d, "chia_se_ytmusic") == null || U.Bool(d, "chia_se_ytmusic");
                c.ShareVideo = U.Get(d, "chia_se_youtube") == null || U.Bool(d, "chia_se_youtube");
                c.ArtTopic = U.Str(d, "kenh_anh") ?? "";
                c.Lang = U.Str(d, "ngon_ngu") ?? "";
            }
            catch (Exception e) { Log.W("Đọc cấu hình lỗi: " + e.Message); }
            return c;
        }

        public void Save()
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            d["kenh"] = Channel;
            d["server"] = Server;
            d["client_id_ytmusic"] = IdMusic;
            d["client_id_youtube"] = IdVideo;
            d["hien_khi_tam_dung"] = ShowPaused;
            d["tam_an"] = Hidden;
            d["nhac_khay"] = Balloon;
            d["trinh_duyet_pc"] = UsePc;
            d["chia_se_ytmusic"] = ShareMusic;
            d["chia_se_youtube"] = ShareVideo;
            d["kenh_anh"] = ArtTopic;
            d["ngon_ngu"] = Lang;
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

        public bool Ready() { return ClientIdFor(null).Length > 0; }

        public bool PhoneReady() { return Regex.IsMatch(Channel ?? "", "^[A-Za-z0-9_-]{6,64}$"); }

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

    static class AutoStart
    {
        const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name = "YT Status";

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

        /// <summary>Đã bật mà .exe bị chuyển chỗ -> cập nhật đường dẫn mới.</summary>
        public static void Refresh()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(Key, true))
                {
                    if (k == null) return;
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
            if (d == null || U.ToLong(U.Get(d, "v"), 0, 99) != 1) throw new FormatException("không phải tin của YT Status");
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
            t.Title = L.T("Bài thử của YT Status", "YT Status test track");
            t.Artist = "YT Status";
            t.Album = L.T("Thử", "Test");
            t.Playing = true;
            t.DurationMs = 200000;
            t.StartMs = U.NowMs() - 30000;
            t.ReceivedMs = U.NowMs();
            t.Cover = "https://i.ytimg.com/vi/dQw4w9WgXcQ/hqdefault.jpg";
            return t;
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

        public DiscordIpc(string clientId) { ClientId = clientId; }

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
            args["pid"] = Process.GetCurrentProcess().Id;
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

        readonly object trackLock = new object();
        readonly object rpcLock = new object();
        readonly AutoResetEvent wake = new AutoResetEvent(false);
        Track track;          // từ điện thoại
        Track pcTrack;        // từ trình duyệt trên PC
        DiscordIpc rpc;
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
                }
                finally { Monitor.Exit(rpcLock); }
            }
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
            wake.Set();
        }

        /// <summary>Đang phát trên PC &gt; đang phát trên điện thoại &gt; tạm dừng trên PC &gt; tạm dừng trên điện thoại.</summary>
        Track Pick() { return Pick(true); }

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
                    Status4(null, () => L.T("Lỗi đọc trình duyệt — xem YTStatus.log", "Cannot read the browser — see YTStatus.log"), null, null);
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
            req.UserAgent = "YTStatus/1.0";
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
                if (myGen != gen) { myGen = gen; lastId = null; lock (trackLock) track = null; wake.Set(); }
                if (!c.PhoneReady())
                {
                    bool had;
                    lock (trackLock) { had = track != null; track = null; }
                    if (had) wake.Set();
                    Status(() => L.T("Không dùng (chưa nhập mã điện thoại)", "Not used (no phone code)"), null, null);
                    Thread.Sleep(1000);
                    continue;
                }
                string url = c.BaseUrl() + "/" + Uri.EscapeDataString(c.Channel) + "/json";
                try
                {
                    if (lastId == null)
                    {
                        // bật app lúc điện thoại đang phát -> lấy ngay tin mới nhất
                        string body = U.HttpGet(url + "?poll=1&since=latest", 20000);
                        string[] lines = body.Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 0) Handle(lines[lines.Length - 1], myGen);
                        lastId = "";
                    }
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url + (lastId.Length > 0 ? "?since=" + lastId : ""));
                    req.UserAgent = "YTStatus/1.0";
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
            bool stop;
            Track t;
            try { t = Track.Parse(U.Str(ev, "message") ?? "", U.ToLong(U.Get(ev, "time"), 0, long.MaxValue / 1000), att, out stop); }
            catch (FormatException) { return id; }   // tin lạ trên kênh -> bỏ qua
            if (t != null) t.FindCover();
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

    // ================================================================ cửa sổ + khay hệ thống
    class MainForm : Form
    {
        readonly EventWaitHandle showEvt;
        bool allowShow;
        bool exiting;
        Engine engine;
        Config cfg;

        NotifyIcon tray;
        ToolStripMenuItem miHide;
        Label lblPhone, lblPc, lblDiscord, lblNow;
        TextBox txtChannel, txtMusic, txtVideo;
        CheckBox chkStartup, chkPaused, chkHidden, chkPc, chkMusic, chkVideo;
        Button btnSave, btnTest, btnTray;
        bool loading;
        // nhãn cần đổi chữ khi đổi ngôn ngữ
        Label lblSub, kPhone, kPc, kDiscord, kNow, fChannel, fMusic, fVideo;
        GroupBox gStatus, gConfig;
        LinkLabel lnkDev;
        ComboBox cboLang;
        ToolStripMenuItem miOpen, miExit, miMusic, miVideo;

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
            allowShow = !startHidden || !cfg.Ready();     // chưa cài đặt xong thì luôn hiện cửa sổ
            Log.W("Khởi động YT Status, cấu hình: " + Config.FilePath);

            BuildUi();
            ApplyLang();
            LoadToUi();
            IntPtr h = Handle;          // tạo handle ngay để nhận trạng thái kể cả khi khởi động ẩn
            GC.KeepAlive(h);

            engine = new Engine(cfg.Copy());
            engine.StatusChanged += OnStatus;
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

        // ------------------------------------------------ dựng giao diện
        void BuildUi()
        {
            SuspendLayout();
            Text = "YT Status";
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            Label title = new Label();
            title.Text = "YT Status";
            title.Font = new Font("Segoe UI Semibold", 15F);
            title.AutoSize = true;
            title.Location = new Point(16, 12);
            Controls.Add(title);

            // nút đổi ngôn ngữ góc trên bên phải
            cboLang = new ComboBox();
            cboLang.DropDownStyle = ComboBoxStyle.DropDownList;
            cboLang.Items.AddRange(new object[] { "Tiếng Việt", "English" });
            cboLang.Location = new Point(318, 18);
            cboLang.Size = new Size(106, 23);
            cboLang.SelectedIndex = L.En ? 1 : 0;
            cboLang.SelectedIndexChanged += delegate
            {
                bool en = cboLang.SelectedIndex == 1;
                if (en == L.En) return;
                L.En = en;
                cfg.Lang = en ? "en" : "vi";
                WriteConfig();
                ApplyLang();
                engine.Cfg = cfg.Copy();
                engine.Relabel();
            };
            Controls.Add(cboLang);

            lblSub = new Label();
            lblSub.ForeColor = Color.FromArgb(100, 104, 112);
            lblSub.AutoSize = false;
            lblSub.Size = new Size(408, 20);
            lblSub.Location = new Point(18, 46);
            Controls.Add(lblSub);

            gStatus = new GroupBox();
            gStatus.Location = new Point(16, 74);
            gStatus.Size = new Size(408, 124);
            Controls.Add(gStatus);
            lblPhone = Row(gStatus, 24, out kPhone);
            lblPc = Row(gStatus, 48, out kPc);
            lblDiscord = Row(gStatus, 72, out kDiscord);
            lblNow = Row(gStatus, 96, out kNow);

            gConfig = new GroupBox();
            gConfig.Location = new Point(16, 208);
            gConfig.Size = new Size(408, 288);
            Controls.Add(gConfig);
            txtChannel = Field(gConfig, 20, out fChannel);
            txtMusic = Field(gConfig, 70, out fMusic);
            txtVideo = Field(gConfig, 120, out fVideo);

            chkStartup = Check(gConfig, 12, 176);
            chkPaused = Check(gConfig, 12, 202);
            chkHidden = Check(gConfig, 214, 176);
            chkPc = Check(gConfig, 12, 228);
            chkPc.Size = new Size(380, 22);
            chkMusic = Check(gConfig, 12, 254);
            chkVideo = Check(gConfig, 214, 254);
            lnkDev = new LinkLabel();
            lnkDev.AutoSize = true;
            lnkDev.Location = new Point(214, 205);
            lnkDev.LinkClicked += delegate { OpenUrl("https://discord.com/developers/applications"); };
            gConfig.Controls.Add(lnkDev);

            btnSave = Btn(16);
            btnTest = Btn(156);
            btnTray = Btn(296);
            btnSave.Click += delegate { SaveFromUi(true); };
            btnTest.Click += delegate
            {
                if (!SaveFromUi(false)) return;
                engine.Demo();
                MessageBox.Show(this, L.T("Đã đặt một bài mẫu lên trạng thái Discord trong 20 giây.\nMở hồ sơ Discord của bạn để xem.",
                                          "A test track is on your Discord status for 20 seconds.\nOpen your Discord profile to see it."),
                    "YT Status", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            btnTray.Click += delegate { HideToTray(); };
            AcceptButton = btnSave;

            chkStartup.CheckedChanged += delegate
            {
                if (loading) return;
                try { AutoStart.Set(chkStartup.Checked); }
                catch (Exception e) { MessageBox.Show(this, L.T("Không ghi được cài đặt khởi động: ", "Could not change the startup setting: ") + e.Message, "YT Status"); }
            };
            chkPaused.CheckedChanged += delegate { if (!loading) SaveFlags(); };
            chkHidden.CheckedChanged += delegate { if (!loading) SaveFlags(); };
            chkPc.CheckedChanged += delegate { if (!loading) SaveFlags(); };
            chkMusic.CheckedChanged += delegate { if (!loading) SaveFlags(); };
            chkVideo.CheckedChanged += delegate { if (!loading) SaveFlags(); };

            // khay hệ thống
            ContextMenuStrip menu = new ContextMenuStrip();
            miOpen = new ToolStripMenuItem();
            miOpen.Font = new Font(menu.Font, FontStyle.Bold);
            miOpen.Click += delegate { ShowFromTray(); };
            miHide = new ToolStripMenuItem();
            miHide.Click += delegate { chkHidden.Checked = !chkHidden.Checked; };
            miMusic = new ToolStripMenuItem("YouTube Music");
            miMusic.Click += delegate { chkMusic.Checked = !chkMusic.Checked; };
            miVideo = new ToolStripMenuItem("YouTube");
            miVideo.Click += delegate { chkVideo.Checked = !chkVideo.Checked; };
            miExit = new ToolStripMenuItem();
            miExit.Click += delegate { ExitApp(); };
            menu.Items.Add(miOpen);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miMusic);
            menu.Items.Add(miVideo);
            menu.Items.Add(miHide);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(miExit);

            tray = new NotifyIcon();
            try { tray.Icon = new Icon(Icon, SystemInformation.SmallIconSize); }
            catch (Exception) { tray.Icon = SystemIcons.Application; }
            tray.Text = "YT Status";
            tray.ContextMenuStrip = menu;
            tray.Visible = true;
            tray.MouseClick += delegate (object s, MouseEventArgs e) { if (e.Button == MouseButtons.Left) ShowFromTray(); };

            ClientSize = new Size(440, 554);
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            ResumeLayout(false);
            PerformLayout();
        }

        Label Row(Control parent, int y, out Label k)
        {
            k = new Label();
            k.ForeColor = Color.FromArgb(100, 104, 112);
            k.Location = new Point(12, y);
            k.Size = new Size(82, 20);
            parent.Controls.Add(k);
            Label v = new Label();
            v.Text = "—";
            v.Location = new Point(96, y);
            v.Size = new Size(300, 20);
            v.AutoEllipsis = true;
            parent.Controls.Add(v);
            return v;
        }

        TextBox Field(Control parent, int y, out Label l)
        {
            l = new Label();
            l.Location = new Point(12, y + 4);
            l.Size = new Size(384, 18);
            parent.Controls.Add(l);
            TextBox t = new TextBox();
            t.Location = new Point(14, y + 24);
            t.Size = new Size(380, 23);
            parent.Controls.Add(t);
            return t;
        }

        CheckBox Check(Control parent, int x, int y)
        {
            CheckBox c = new CheckBox();
            c.Location = new Point(x, y);
            c.Size = new Size(190, 22);
            parent.Controls.Add(c);
            return c;
        }

        Button Btn(int x)
        {
            Button b = new Button();
            b.Location = new Point(x, 508);
            b.Size = new Size(128, 32);
            b.FlatStyle = FlatStyle.System;      // nút chuẩn Windows, chữ căn giữa
            b.TextAlign = ContentAlignment.MiddleCenter;
            Controls.Add(b);
            return b;
        }

        /// <summary>Đặt toàn bộ chữ trên cửa sổ + menu khay theo ngôn ngữ đang chọn.</summary>
        void ApplyLang()
        {
            lblSub.Text = L.T("Đang nghe / xem gì trên YouTube → trạng thái Discord.", "What you play on YouTube → your Discord status.");
            gStatus.Text = L.T("Trạng thái", "Status");
            kPhone.Text = L.T("Điện thoại", "Phone");
            kPc.Text = L.T("Máy tính", "Computer");
            kDiscord.Text = "Discord";
            kNow.Text = L.T("Đang hiện", "Showing");
            gConfig.Text = L.T("Cài đặt", "Settings");
            fChannel.Text = L.T("Mã điện thoại (tuỳ chọn — chỉ nghe trên PC thì để trống)", "Phone code (optional — leave empty if PC only)");
            fMusic.Text = L.T("Client ID ứng dụng Discord tên \"YouTube Music\"", "Client ID of your Discord app named \"YouTube Music\"");
            fVideo.Text = L.T("Client ID ứng dụng tên \"YouTube\" (tuỳ chọn)", "Client ID of an app named \"YouTube\" (optional)");
            chkStartup.Text = L.T("Chạy cùng Windows", "Start with Windows");
            chkPaused.Text = L.T("Vẫn hiện khi tạm dừng", "Show while paused");
            chkHidden.Text = L.T("Tắt hiện lên Discord", "Don't show on Discord");
            chkPc.Text = PcMedia.Supported
                ? L.T("Nhận YouTube trên trình duyệt PC", "Detect YouTube in this PC's browser")
                : L.T("Nhận YouTube trên trình duyệt PC (cần Windows 10 1809+)", "Detect YouTube in this PC's browser (needs Windows 10 1809+)");
            lnkDev.Text = L.T("Tạo ứng dụng Discord…", "Create a Discord app…");
            btnSave.Text = L.T("Lưu", "Save");
            btnTest.Text = L.T("Thử Discord", "Test Discord");
            btnTray.Text = L.T("Ẩn xuống khay", "Hide to tray");
            miOpen.Text = L.T("Mở YT Status", "Open YT Status");
            miHide.Text = L.T("Tắt hiện lên Discord", "Don't show on Discord");
            chkMusic.Text = L.T("Chia sẻ YouTube Music", "Share YouTube Music");
            chkVideo.Text = L.T("Chia sẻ YouTube", "Share YouTube");
            miMusic.Text = L.T("Chia sẻ YouTube Music", "Share YouTube Music");
            miVideo.Text = L.T("Chia sẻ YouTube", "Share YouTube");
            miExit.Text = L.T("Thoát", "Exit");
        }

        // ------------------------------------------------ dữ liệu <-> giao diện
        void LoadToUi()
        {
            loading = true;
            txtChannel.Text = cfg.Channel;
            txtMusic.Text = cfg.IdMusic;
            txtVideo.Text = cfg.IdVideo;
            chkPaused.Checked = cfg.ShowPaused;
            chkHidden.Checked = cfg.Hidden;
            miHide.Checked = cfg.Hidden;
            chkPc.Checked = cfg.UsePc;
            chkMusic.Checked = cfg.ShareMusic;
            chkVideo.Checked = cfg.ShareVideo;
            miMusic.Checked = cfg.ShareMusic;
            miVideo.Checked = cfg.ShareVideo;
            if (!PcMedia.Supported) chkPc.Enabled = false;
            try { chkStartup.Checked = AutoStart.IsOn(); } catch (Exception) { }
            loading = false;
        }

        bool SaveFromUi(bool notify)
        {
            string ch = txtChannel.Text.Trim();
            string m = txtMusic.Text.Trim();
            string v = txtVideo.Text.Trim();
            if (ch.Length > 0 && !Regex.IsMatch(ch, "^[A-Za-z0-9_-]{6,64}$"))
            {
                Warn(L.T("Mã điện thoại chưa đúng (vd nc-abcd-efgh-jkmn), hoặc để trống nếu chỉ nghe trên PC.",
                         "The phone code looks wrong (e.g. nc-abcd-efgh-jkmn). Leave it empty if you only use the PC."), txtChannel);
                return false;
            }
            if (!Regex.IsMatch(m, @"^\d{17,20}$") && !(m.Length == 0 && Regex.IsMatch(v, @"^\d{17,20}$")))
            {
                Warn(L.T("Client ID là dãy 17–20 chữ số (Developer Portal › ứng dụng › General Information › Application ID).",
                         "A Client ID is 17–20 digits (Developer Portal › your app › General Information › Application ID)."), txtMusic);
                return false;
            }
            if (v.Length > 0 && !Regex.IsMatch(v, @"^\d{17,20}$"))
            {
                Warn(L.T("Client ID \"YouTube\" là dãy 17–20 chữ số, hoặc để trống.", "The \"YouTube\" Client ID must be 17–20 digits, or empty."), txtVideo);
                return false;
            }
            bool changed = ch != cfg.Channel || m != cfg.IdMusic || v != cfg.IdVideo;
            cfg.Channel = ch;
            cfg.IdMusic = m;
            cfg.IdVideo = v;
            cfg.ShowPaused = chkPaused.Checked;
            cfg.Hidden = chkHidden.Checked;
            cfg.UsePc = chkPc.Checked;
            cfg.ShareMusic = chkMusic.Checked;
            cfg.ShareVideo = chkVideo.Checked;
            if (!WriteConfig()) return false;
            if (changed) engine.Reconfigure(cfg.Copy());
            else { engine.Cfg = cfg.Copy(); engine.Poke(); }
            if (notify) tray.ShowBalloonTip(2000, "YT Status", L.T("Đã lưu.", "Saved."), ToolTipIcon.Info);
            return true;
        }

        void SaveFlags()
        {
            cfg.ShowPaused = chkPaused.Checked;
            cfg.Hidden = chkHidden.Checked;
            cfg.UsePc = chkPc.Checked;
            cfg.ShareMusic = chkMusic.Checked;
            cfg.ShareVideo = chkVideo.Checked;
            miHide.Checked = cfg.Hidden;
            miMusic.Checked = cfg.ShareMusic;
            miVideo.Checked = cfg.ShareVideo;
            WriteConfig();
            engine.Cfg = cfg.Copy();
            engine.Poke();
        }

        bool WriteConfig()
        {
            try { cfg.Save(); return true; }
            catch (Exception e)
            {
                MessageBox.Show(this, L.T("Không lưu được ", "Could not save ") + Config.FilePath + ":\n" + e.Message, "YT Status", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        void Warn(string msg, Control focus)
        {
            ShowFromTray();
            MessageBox.Show(this, msg, "YT Status", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            focus.Focus();
        }

        // trạng thái mới nhất; cửa sổ đang ẩn thì chỉ cập nhật chữ ở khay, mở cửa sổ ra mới vẽ các dòng
        string[] pending;
        string lastTip;

        void OnStatus(string phone, string pc, string discord, string now)
        {
            if (!IsHandleCreated) return;
            try
            {
                BeginInvoke(new Action(delegate
                {
                    pending = new string[] { phone, pc, discord, now };
                    string tip = "YT Status · " + now;
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

        void PaintStatus()
        {
            string[] p = pending;
            if (p == null) return;
            lblPhone.Text = p[0];
            lblPc.Text = p[1];
            lblDiscord.Text = p[2];
            lblNow.Text = p[3];
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
                tray.ShowBalloonTip(4000, L.T("YT Status vẫn đang chạy", "YT Status is still running"),
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
            Log.W("Thoát YT Status");
            try { engine.Stop(); } catch (Exception) { }
            tray.Visible = false;
            tray.Dispose();
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(url); } catch (Exception) { }
        }
    }
}
