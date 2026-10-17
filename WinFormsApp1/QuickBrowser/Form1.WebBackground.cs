using System;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;

namespace QuickBrowser
{
    // 網頁背景：資料夾背景路徑填網址（http:// 或 https://）時，用 WebView2 當檔案區的底。
    // WebView2 放在檔案區正下方一層，檔案區切換成分層視窗（LayeredPictureBox.Layered），
    // 底色全透明，所以卡片疊在網頁上；點到卡片以外的透明處會直接操作網頁。
    // 本機影片檔（直接填路徑即可）：用 <video> 鋪滿、靜音循環，禁止互動。
    // YouTube 網址：改開嵌入播放頁，影片鋪滿、鎖最高畫質、靜音循環，且禁止與網頁互動。
    public partial class Form1
    {
        WebView2 webBackground;
        volatile bool webBackgroundActive;
        // YouTube 背景：檔案區底色用 alpha = 1（看不出來），點擊不會穿透到網頁
        volatile bool webBackgroundBlockInput;
        string webBackgroundUrl;

        static bool isWebBackgroundUrl(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            path = path.Trim();
            return path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        // 背景影片：WebView2（Edge 核心）能播的格式；avi / wmv 等瀏覽器不支援
        static readonly string[] webVideoExtensions = { ".mp4", ".webm", ".mov", ".m4v", ".mkv", ".ogv", ".ogg" };

        /// <summary>背景路徑是存在的影片檔就回傳完整路徑（可直接填路徑，也接受 file:/// 與前後引號），否則 null。</summary>
        static string videoBackgroundPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            path = path.Trim().Trim('"');
            try
            {
                if (path.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) path = new Uri(path).LocalPath;
                path = System.IO.Path.GetFullPath(path);
            }
            catch { return null; }
            string ext = System.IO.Path.GetExtension(path);
            if (Array.FindIndex(webVideoExtensions, e => e.Equals(ext, StringComparison.OrdinalIgnoreCase)) < 0) return null;
            return System.IO.File.Exists(path) ? path : null;
        }

        static readonly Regex youtubeIdRegex = new Regex(
            @"(?:youtube\.com/(?:watch\?(?:.*&)?v=|embed/|shorts/|live/|v/)|youtu\.be/)([A-Za-z0-9_-]{11})",
            RegexOptions.IgnoreCase);

        /// <summary>YouTube 影片網址取出影片 ID；不是 YouTube 影片回傳 null。</summary>
        static string youtubeVideoId(string url)
        {
            var m = youtubeIdRegex.Match(url ?? "");
            return m.Success ? m.Groups[1].Value : null;
        }

        // YouTube 不接受沒有來源網址（Referer）的嵌入（錯誤 153），所以不能直接開 embed 網址：
        // 在 WebView2 掛一個虛擬網域，放一頁本機 HTML，用 iframe 正規嵌入影片。
        // iframe 做成 16:9 的 cover 尺寸並置中，影片就會鋪滿檔案區、不留黑邊
        const string webBackgroundHost = "quickbrowser.example";
        const string youtubeBackgroundPage = @"<!doctype html><html><head><meta charset=""utf-8"">
<style>
html,body{margin:0;height:100%;overflow:hidden;background:#000}
iframe{position:absolute;left:50%;top:50%;width:max(100vw,177.78vh);height:max(100vh,56.25vw);
 transform:translate(-50%,-50%);border:0;pointer-events:none}
</style></head><body><script>
var id = new URLSearchParams(location.search).get('v') || '';
var f = document.createElement('iframe');
f.allow = 'autoplay; encrypted-media';
f.referrerPolicy = 'strict-origin-when-cross-origin';
f.src = 'https://www.youtube.com/embed/' + encodeURIComponent(id) +
 '?autoplay=1&mute=1&loop=1&playlist=' + encodeURIComponent(id) +
 '&controls=0&disablekb=1&fs=0&iv_load_policy=3&modestbranding=1&rel=0&playsinline=1&enablejsapi=1' +
 '&origin=' + encodeURIComponent(location.origin);
document.body.appendChild(f);
</script></body></html>";

        // 本機影片：影片所在資料夾掛到另一個虛擬網域，頁面用 <video> 鋪滿、靜音、循環播放
        const string webVideoHost = "quickbrowser-video.example";
        const string videoBackgroundPage = @"<!doctype html><html><head><meta charset=""utf-8"">
<style>
html,body{margin:0;height:100%;overflow:hidden;background:#000}
video{position:absolute;left:0;top:0;width:100%;height:100%;object-fit:cover;pointer-events:none}
</style></head><body><video autoplay muted loop playsinline disablepictureinpicture></video><script>
var v = document.querySelector('video');
v.src = new URLSearchParams(location.search).get('src') || '';
var play = function(){ v.play().catch(function(){}); };
play();
setInterval(function(){ if (v.paused) play(); }, 1000);
</script></body></html>";

        static string webBackgroundFolder => System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickBrowser");

        // 注入到每個新文件（含 iframe）：只在「被 QuickBrowser 背景頁嵌入的 YouTube 播放器」裡動作。
        // 播放器裡影片以外的東西（標題、頻道、YouTube 標誌、分享、暫停畫面…）一律隱藏，YouTube 改版也不會漏；
        // 禁止滑鼠互動；每秒把畫質鎖在可用的最高等級，被暫停或播完就接著播
        const string youtubeBackgroundScript = @"(function(){
 if (window.__qbBackground) return;
 if (!/(^|\.)youtube(-nocookie)?\.com$/.test(location.hostname) || location.pathname.indexOf('/embed/') !== 0) return;
 if ((document.referrer || '').indexOf('quickbrowser.example') < 0) return;
 window.__qbBackground = 1;
 var st = document.createElement('style');
 st.textContent =
  'html,body{overflow:hidden!important;background:#000!important;pointer-events:none!important;cursor:default!important}' +
  '#movie_player,.html5-video-player{background:#000!important}' +
  '#movie_player > :not(.html5-video-container){display:none!important}' +
  'video{object-fit:cover!important;width:100vw!important;height:100vh!important;left:0!important;top:0!important}';
 var add = function(){ (document.head || document.documentElement).appendChild(st); };
 if (document.documentElement) add(); else document.addEventListener('DOMContentLoaded', add);
 setInterval(function(){
  if (!st.isConnected) add();
  var p = document.getElementById('movie_player');
  if (!p || !p.getAvailableQualityLevels) return;
  try {
   if (p.isMuted && !p.isMuted()) p.mute();
   var levels = p.getAvailableQualityLevels();
   if (levels && levels.length) {
    var best = levels[0] === 'auto' && levels.length > 1 ? levels[1] : levels[0];
    if (p.getPlaybackQuality() !== best) {
     if (p.setPlaybackQualityRange) p.setPlaybackQualityRange(best, best);
     if (p.setPlaybackQuality) p.setPlaybackQuality(best);
    }
   }
   var s = p.getPlayerState ? p.getPlayerState() : 1;
   if (s === 0) { p.seekTo(0, true); p.playVideo(); }
   else if (s === 2 || s === -1 || s === 5) p.playVideo();
  } catch (e) {}
 }, 1000);
})();";

        /// <summary>切換網頁背景（UI 執行緒）。url 為 null 表示關閉。</summary>
        void setWebBackground(string url)
        {
            if (IsDisposed) return;
            if (url == null)
            {
                if (!webBackgroundActive) return;
                webBackgroundActive = false;
                webBackgroundBlockInput = false;
                webBackgroundUrl = null;
                drawPanel.Layered = false;
                if (webBackground != null)
                {
                    webBackground.Visible = false;
                    // 停在空白頁，背景影片／聲音不會在背後繼續跑
                    try { webBackground.CoreWebView2?.Navigate("about:blank"); } catch { }
                }
                setToDraw();
                return;
            }

            if (webBackground == null)
            {
                webBackground = new WebView2
                {
                    TabStop = false,
                    DefaultBackgroundColor = System.Drawing.Color.Black,
                    // 背景影片不需使用者點擊就能自動播放
                    CreationProperties = new CoreWebView2CreationProperties
                    {
                        AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required",
                        // 瀏覽器快取放在使用者資料夾（預設在 exe 旁邊，會塞進專案的 bin 並干擾 dotnet watch）
                        UserDataFolder = System.IO.Path.Combine(webBackgroundFolder, "WebView2"),
                    },
                };
                Controls.Add(webBackground);
                // 整批顯示／隱藏控制項的程式（看圖、最大化全螢幕、還原）也會碰到它：
                // 一律以檔案區的狀態為準，該顯示就顯示、不該顯示就隱藏
                webBackground.VisibleChanged += (s, e) =>
                {
                    bool show = webBackgroundShouldShow();
                    if (webBackground.Visible != show) webBackground.Visible = show;
                };
            }

            string videoId = youtubeVideoId(url);
            // YouTube 與本機影片都是純背景：禁止互動
            webBackgroundBlockInput = videoId != null || !isWebBackgroundUrl(url);
            webBackgroundActive = true;
            if (webBackgroundUrl != url)
            {
                webBackgroundUrl = url;
                navigateWebBackground(url, videoId);
            }
            drawPanel.Layered = true;
            syncWebBackground();
            setToDraw();
        }

        bool webBackgroundReady;

        async void navigateWebBackground(string url, string videoId)
        {
            try
            {
                if (!webBackgroundReady)
                {
                    await webBackground.EnsureCoreWebView2Async();
                    var core = webBackground.CoreWebView2;
                    string pageFolder = System.IO.Path.Combine(webBackgroundFolder, "WebBackground");
                    System.IO.Directory.CreateDirectory(pageFolder);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(pageFolder, "youtube.html"), youtubeBackgroundPage);
                    System.IO.File.WriteAllText(System.IO.Path.Combine(pageFolder, "video.html"), videoBackgroundPage);
                    core.SetVirtualHostNameToFolderMapping(webBackgroundHost, pageFolder,
                        Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                    // 影片在 iframe 裡：每個新文件（含 iframe）建立時就注入，腳本自己判斷是不是背景用的 YouTube 播放器
                    await core.AddScriptToExecuteOnDocumentCreatedAsync(youtubeBackgroundScript);
                    webBackgroundReady = true;
                }
                // 等待初始化期間可能又換了背景
                if (webBackgroundUrl != url) return;
                var core2 = webBackground.CoreWebView2;
                if (videoId != null)
                {
                    core2.Navigate($"https://{webBackgroundHost}/youtube.html?v={videoId}");
                }
                else if (!isWebBackgroundUrl(url))
                {
                    // 本機影片：把影片所在資料夾掛上虛擬網域（換資料夾時重新對應）
                    string dir = System.IO.Path.GetDirectoryName(url);
                    try { core2.ClearVirtualHostNameToFolderMapping(webVideoHost); } catch { }
                    core2.SetVirtualHostNameToFolderMapping(webVideoHost, dir,
                        Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                    string src = $"https://{webVideoHost}/" + Uri.EscapeDataString(System.IO.Path.GetFileName(url));
                    core2.Navigate($"https://{webBackgroundHost}/video.html?src=" + Uri.EscapeDataString(src));
                }
                else
                {
                    core2.Navigate(new Uri(url).AbsoluteUri);
                }
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Hand.Play();
                Text = ex.Message;
            }
        }

        bool webBackgroundShouldShow() =>
            webBackgroundActive && _drawPanel != null && _drawPanel.Visible;

        /// <summary>讓網頁背景與檔案區同位置、同大小，並固定在檔案區正下方一層。</summary>
        void syncWebBackground()
        {
            if (webBackground == null || _drawPanel == null) return;
            bool show = webBackgroundShouldShow();
            if (show)
            {
                webBackground.Dock = _drawPanel.Dock;
                if (_drawPanel.Dock == DockStyle.None) webBackground.Bounds = _drawPanel.Bounds;
                int panelIndex = Controls.GetChildIndex(_drawPanel);
                int webIndex = Controls.GetChildIndex(webBackground);
                if (webIndex != panelIndex + 1)
                    Controls.SetChildIndex(webBackground, webIndex < panelIndex ? panelIndex : panelIndex + 1);
            }
            if (webBackground.Visible != show) webBackground.Visible = show;
        }
    }
}
