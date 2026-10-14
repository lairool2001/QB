using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickBrowser
{
    /// <summary>
    /// 以自繪標題列取代系統標題列，同時保留原生行為：
    /// 拖曳移動、雙擊最大化、Aero Snap、Win11 Snap Layouts（hover 最大化鈕）、右鍵系統選單、邊緣縮放。
    ///
    /// 做法：WM_NCCALCSIZE 把系統標題列併入工作區，TitleBar 放在工作區最上方，
    /// 其 WM_NCHITTEST 回傳 HTTRANSPARENT，交由表單回報 HTCAPTION / HTMINBUTTON / HTMAXBUTTON / HTCLOSE。
    /// Form 只需在 WndProc 呼叫 PreProcess / PostProcess。
    /// </summary>
    public sealed class WindowChrome
    {
        #region Win32

        const int WM_NCCALCSIZE = 0x0083;
        const int WM_NCHITTEST = 0x0084;
        const int WM_NCMOUSEMOVE = 0x00A0;
        const int WM_NCLBUTTONDOWN = 0x00A1;
        const int WM_NCLBUTTONUP = 0x00A2;
        const int WM_NCLBUTTONDBLCLK = 0x00A3;
        const int WM_NCMOUSELEAVE = 0x02A2;
        const int WM_SYSCOMMAND = 0x0112;

        internal const int HTTRANSPARENT = -1;
        internal const int HTCLIENT = 1;
        internal const int HTCAPTION = 2;
        internal const int HTMINBUTTON = 8;
        internal const int HTMAXBUTTON = 9;
        const int HTTOP = 12;
        const int HTTOPLEFT = 13;
        const int HTTOPRIGHT = 14;
        internal const int HTCLOSE = 20;

        const int SC_MINIMIZE = 0xF020;
        const int SC_MAXIMIZE = 0xF030;
        const int SC_RESTORE = 0xF120;
        const int SC_CLOSE = 0xF060;

        const int SM_CYCAPTION = 4;
        const int SM_CYSIZEFRAME = 33;
        const int SM_CXPADDEDBORDER = 92;

        const uint TME_LEAVE = 0x2;
        const uint TME_NONCLIENT = 0x10;

        const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOZORDER = 0x4, SWP_NOACTIVATE = 0x10, SWP_FRAMECHANGED = 0x20;

        const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        const int DWMWA_BORDER_COLOR = 34;
        const int DWMWCP_ROUND = 2;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct TRACKMOUSEEVENT
        {
            public uint cbSize;
            public uint dwFlags;
            public IntPtr hwndTrack;
            public uint dwHoverTime;
        }

        [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr h);
        [DllImport("user32.dll")] static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT tme);
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, IntPtr l);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);

        #endregion

        readonly Form _form;
        int _pendingTop;
        bool _trackingNc;
        bool _sizeCorrected;

        public TitleBar Bar { get; }

        /// <summary>自訂外框是否生效（無邊框全螢幕時停用）。</summary>
        public bool Enabled => _form.FormBorderStyle != FormBorderStyle.None;

        /// <summary>工作區內容應從此 Y 座標開始（標題列下緣）。</summary>
        public int ContentTop => Bar.Visible ? Bar.Height : 0;

        public WindowChrome(Form form)
        {
            _form = form;
            Bar = new TitleBar(this, form);
            form.Controls.Add(Bar);
            Bar.BringToFront();

            form.HandleCreated += (s, e) => OnHandleCreated();
            form.StyleChanged += (s, e) => Sync();
            form.Resize += (s, e) => { TrackNormalSize(); Sync(); Bar.Invalidate(); };
            form.Activated += (s, e) => Bar.WindowActive = true;
            form.Deactivate += (s, e) => Bar.WindowActive = false;
            form.TextChanged += (s, e) => Bar.OnTitleChanged();
            Sync();
        }

        Size _normalSize;
        bool _inSysCommand;

        void TrackNormalSize()
        {
            // 還原過程中的中間尺寸不算
            if (!_inSysCommand && _sizeCorrected && Enabled && _form.WindowState == FormWindowState.Normal)
                _normalSize = _form.Size;
        }

        void Sync()
        {
            bool show = Enabled;
            Bar.Allowed = show;
            Bar.Visible = show;
            // 錨定控制項以 DisplayRectangle 為基準，所以 Padding 會把整個內容區往下推一個標題列高度；
            // Designer 座標即為「內容區座標」。標題列本身不受 Padding 影響，每次重新釘回頂端。
            var padding = new Padding(0, show ? Theme.TitleBarHeight : 0, 0, 0);
            if (_form.Padding != padding) _form.Padding = padding;
            var bounds = new Rectangle(0, 0, _form.ClientSize.Width, Theme.TitleBarHeight);
            if (Bar.Bounds != bounds) Bar.Bounds = bounds;
        }

        /// <summary>
        /// 外框尺寸是以「含系統標題列」算出的；標題列併入工作區後扣回去，讓工作區維持設計大小。
        /// 只在第一次建立 handle 後呼叫一次。
        /// </summary>
        public void CorrectInitialHeight()
        {
            if (_sizeCorrected || !Enabled || _form.WindowState != FormWindowState.Normal) return;
            _sizeCorrected = true;
            _form.Height -= SystemTopFrame();
            _normalSize = _form.Size;
        }

        void OnHandleCreated()
        {
            IntPtr h = _form.Handle;
            int on = 1, corner = DWMWCP_ROUND;
            int border = ColorTranslator.ToWin32(Color.FromArgb(52, 52, 78));
            DwmSetWindowAttribute(h, DWMWA_USE_IMMERSIVE_DARK_MODE, ref on, sizeof(int));
            DwmSetWindowAttribute(h, DWMWA_WINDOW_CORNER_PREFERENCE, ref corner, sizeof(int));
            DwmSetWindowAttribute(h, DWMWA_BORDER_COLOR, ref border, sizeof(int));

            SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_NOACTIVATE | SWP_FRAMECHANGED);

            Sync();
        }

        /// <summary>系統標題列 + 上邊框高度（已被併入工作區的部分）。</summary>
        public static int SystemTopFrame() =>
            GetSystemMetrics(SM_CYCAPTION) + GetSystemMetrics(SM_CYSIZEFRAME) + GetSystemMetrics(SM_CXPADDEDBORDER);

        static int ResizeFrame() =>
            GetSystemMetrics(SM_CYSIZEFRAME) + GetSystemMetrics(SM_CXPADDEDBORDER);

        /// <summary>在 base.WndProc 之前呼叫；回傳 true 表示訊息已處理、不要再交給 base。</summary>
        public bool PreProcess(ref Message m)
        {
            if (!Enabled) return false;

            switch (m.Msg)
            {
                case WM_SYSCOMMAND:
                    _inSysCommand = true;
                    return false;

                case WM_NCCALCSIZE:
                    if (m.WParam != IntPtr.Zero)
                        _pendingTop = Marshal.PtrToStructure<RECT>(m.LParam).Top;
                    return false;

                case WM_NCMOUSEMOVE:
                    Bar.HotButton = ButtonOf(m.WParam);
                    if (!_trackingNc)
                    {
                        var tme = new TRACKMOUSEEVENT
                        {
                            cbSize = (uint)Marshal.SizeOf<TRACKMOUSEEVENT>(),
                            dwFlags = TME_LEAVE | TME_NONCLIENT,
                            hwndTrack = _form.Handle,
                        };
                        _trackingNc = TrackMouseEvent(ref tme);
                    }
                    return false;

                case WM_NCMOUSELEAVE:
                    _trackingNc = false;
                    Bar.HotButton = 0;
                    Bar.PressedButton = 0;
                    return false;

                case WM_NCLBUTTONDOWN:
                    if (ButtonOf(m.WParam) != 0)
                    {
                        // 自行處理，避免系統畫出舊式標題鈕
                        Bar.PressedButton = ButtonOf(m.WParam);
                        m.Result = IntPtr.Zero;
                        return true;
                    }
                    return false;

                case WM_NCLBUTTONUP:
                    int up = ButtonOf(m.WParam);
                    if (up != 0)
                    {
                        bool clicked = up == Bar.PressedButton;
                        Bar.PressedButton = 0;
                        if (clicked) Execute(up);
                        m.Result = IntPtr.Zero;
                        return true;
                    }
                    Bar.PressedButton = 0;
                    return false;

                case WM_NCLBUTTONDBLCLK:
                    if (ButtonOf(m.WParam) != 0)
                    {
                        m.Result = IntPtr.Zero;
                        return true;
                    }
                    return false;
            }
            return false;
        }

        /// <summary>在 base.WndProc 之後呼叫，用來修正系統計算出的結果。</summary>
        public void PostProcess(ref Message m)
        {
            if (m.Msg == WM_SYSCOMMAND)
            {
                _inSysCommand = false;
                // Form.RestoreWindowBoundsIfNecessary 用內部公式（含系統標題列）把 ClientSize 換回視窗大小，
                // 每次還原都會長高一個標題列；在它跑完之後套回記住的一般大小
                if (Enabled && _form.WindowState == FormWindowState.Normal && !_normalSize.IsEmpty && _form.Size != _normalSize)
                    _form.Size = _normalSize;
                return;
            }

            if (!Enabled) return;

            switch (m.Msg)
            {
                case WM_NCCALCSIZE:
                    if (m.WParam == IntPtr.Zero) return;
                    var rc = Marshal.PtrToStructure<RECT>(m.LParam);
                    // 最大化時視窗會超出螢幕一個邊框厚度，需內縮以免標題列被裁掉
                    rc.Top = _pendingTop + (IsMaximized ? ResizeFrame() : 0);
                    Marshal.StructureToPtr(rc, m.LParam, false);
                    m.Result = IntPtr.Zero;
                    break;

                case WM_NCHITTEST:
                    if (m.Result.ToInt64() != HTCLIENT) return;
                    long lp = m.LParam.ToInt64();
                    var pt = _form.PointToClient(new Point((short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF)));

                    if (!IsMaximized && pt.Y >= 0 && pt.Y < 6)
                    {
                        if (pt.X < 14) m.Result = (IntPtr)HTTOPLEFT;
                        else if (pt.X >= _form.ClientSize.Width - 14) m.Result = (IntPtr)HTTOPRIGHT;
                        else m.Result = (IntPtr)HTTOP;
                        return;
                    }

                    if (Bar.Visible && pt.Y >= 0 && pt.Y < Bar.Height)
                        m.Result = (IntPtr)Bar.HitTest(pt.X);
                    break;
            }
        }

        // WM_NCCALCSIZE 期間 Form.WindowState 尚未更新，直接問系統
        bool IsMaximized => _form.IsHandleCreated ? IsZoomed(_form.Handle) : _form.WindowState == FormWindowState.Maximized;

        static int ButtonOf(IntPtr wParam)
        {
            int ht = wParam.ToInt32();
            return ht == HTMINBUTTON || ht == HTMAXBUTTON || ht == HTCLOSE ? ht : 0;
        }

        void Execute(int button)
        {
            int cmd;
            switch (button)
            {
                case HTMINBUTTON: cmd = SC_MINIMIZE; break;
                case HTMAXBUTTON: cmd = IsMaximized ? SC_RESTORE : SC_MAXIMIZE; break;
                case HTCLOSE: cmd = SC_CLOSE; break;
                default: return;
            }
            // 走系統命令，保留最小化/最大化動畫與 FormClosing 流程
            SendMessage(_form.Handle, WM_SYSCOMMAND, (IntPtr)cmd, IntPtr.Zero);
        }
    }

    /// <summary>自繪標題列：圖示、名稱、狀態文字與 Fluent 風格的最小化/最大化/關閉鈕。</summary>
    public sealed class TitleBar : Control
    {
        const int ButtonWidth = 46;
        static readonly Font GlyphFont = new Font(Theme.IconFont.FontFamily, 7.5f);
        const string AppName = "QuickBrowser";

        readonly WindowChrome _chrome;
        readonly Form _form;
        readonly Tween _min, _max, _close, _active, _status;
        int _hot, _pressed;
        bool _windowActive = true;

        internal bool Allowed = true;

        internal TitleBar(WindowChrome chrome, Form form)
        {
            _chrome = chrome;
            _form = form;
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            Height = Theme.TitleBarHeight;
            Bounds = new Rectangle(0, 0, form.ClientSize.Width, Theme.TitleBarHeight);
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            BackColor = Theme.TitleBar;

            _min = new Tween(Invalidate, 0.3f);
            _max = new Tween(Invalidate, 0.3f);
            _close = new Tween(Invalidate, 0.3f);
            _active = new Tween(Invalidate, 0.2f);
            _status = new Tween(Invalidate, 0.18f);
            _active.Snap(1f);
            _status.Snap(1f);
        }

        // 外部程式碼常以 foreach Controls 隱藏全部控制項；標題列只隨外框狀態顯示
        protected override void SetVisibleCore(bool value) => base.SetVisibleCore(Allowed);

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0084) // WM_NCHITTEST：交給表單判斷
            {
                m.Result = (IntPtr)WindowChrome.HTTRANSPARENT;
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            if (Parent != null) Width = Parent.ClientSize.Width;
        }

        internal int HotButton
        {
            get => _hot;
            set
            {
                if (_hot == value) return;
                _hot = value;
                _min.To(value == WindowChrome.HTMINBUTTON ? 1f : 0f);
                _max.To(value == WindowChrome.HTMAXBUTTON ? 1f : 0f);
                _close.To(value == WindowChrome.HTCLOSE ? 1f : 0f);
            }
        }

        internal int PressedButton
        {
            get => _pressed;
            set { if (_pressed != value) { _pressed = value; Invalidate(); } }
        }

        internal bool WindowActive
        {
            get => _windowActive;
            set { _windowActive = value; _active.To(value ? 1f : 0f); }
        }

        internal void OnTitleChanged()
        {
            // 狀態文字更新時淡入，讓使用者注意到變化
            _status.Snap(0f);
            _status.To(1f);
        }

        internal int HitTest(int x)
        {
            int w = Width;
            if (x >= w - ButtonWidth) return WindowChrome.HTCLOSE;
            if (x >= w - ButtonWidth * 2) return WindowChrome.HTMAXBUTTON;
            if (x >= w - ButtonWidth * 3) return WindowChrome.HTMINBUTTON;
            return WindowChrome.HTCAPTION;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _min.Dispose(); _max.Dispose(); _close.Dispose(); _active.Dispose(); _status.Dispose();
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Theme.HighQuality(g);
            float a = _active.Value;

            using (var b = new SolidBrush(Theme.TitleBar))
                g.FillRectangle(b, ClientRectangle);
            using (var p = new Pen(Theme.Lerp(Theme.Base, Theme.Divider, 0.6f + 0.4f * a)))
                g.DrawLine(p, 0, Height - 1, Width, Height - 1);

            // ── 左側：圖示 + 名稱 + 狀態 ─────────────────────
            int x = 12;
            if (_form.Icon != null)
            {
                var iconRect = new Rectangle(x, (Height - 16) / 2, 16, 16);
                using (var icon = new Icon(_form.Icon, 16, 16))
                    g.DrawIcon(icon, iconRect);
                x += 16 + 10;
            }

            var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding;
            int rightLimit = Width - ButtonWidth * 3 - 12;
            var nameColor = Theme.Lerp(Theme.TextMuted, Theme.Text, a);
            int nameW = TextRenderer.MeasureText(g, AppName, Theme.TitleFont, Size.Empty, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            TextRenderer.DrawText(g, AppName, Theme.TitleFont, new Rectangle(x, 0, Math.Max(0, Math.Min(nameW, rightLimit - x)), Height), nameColor, flags | TextFormatFlags.EndEllipsis);
            x += nameW;

            string status = _form.Text;
            if (!string.IsNullOrEmpty(status) && status != AppName && x + 24 < rightLimit)
            {
                // 小圓點分隔 + 狀態文字（Form.Text 在本程式中被當作狀態列使用）
                x += 10;
                using (var b = new SolidBrush(Theme.WithAlpha(Theme.Accent, (int)(120 + 135 * a))))
                    g.FillEllipse(b, x, Height / 2f - 2.5f, 5, 5);
                x += 5 + 10;
                var statusColor = Theme.Lerp(Theme.Base, Theme.Lerp(Theme.TextMuted, Theme.TextSecondary, a), _status.Value);
                TextRenderer.DrawText(g, status, Theme.StatusFont,
                    new Rectangle(x, 0, Math.Max(0, rightLimit - x), Height), statusColor,
                    flags | TextFormatFlags.EndEllipsis);
            }

            // ── 右側：標題鈕 ─────────────────────────────────
            bool maximized = _form.WindowState == FormWindowState.Maximized;
            DrawCaptionButton(g, Width - ButtonWidth * 3, Theme.Glyph.Minimize, _min.Value, _pressed == WindowChrome.HTMINBUTTON, false, a);
            DrawCaptionButton(g, Width - ButtonWidth * 2, maximized ? Theme.Glyph.Restore : Theme.Glyph.Maximize, _max.Value, _pressed == WindowChrome.HTMAXBUTTON, false, a);
            DrawCaptionButton(g, Width - ButtonWidth, Theme.Glyph.Close, _close.Value, _pressed == WindowChrome.HTCLOSE, true, a);
        }

        void DrawCaptionButton(Graphics g, int x, string glyph, float hover, bool pressed, bool isClose, float active)
        {
            var rect = new Rectangle(x, 0, ButtonWidth, Height - 1);
            Color glyphColor = Theme.Lerp(Theme.TextMuted, Theme.Text, Math.Max(active, hover));

            if (isClose)
            {
                if (hover > 0.01f || pressed)
                {
                    var red = pressed ? Theme.Lerp(Theme.CloseHover, Color.Black, 0.2f) : Theme.CloseHover;
                    using (var b = new SolidBrush(Theme.WithAlpha(red, (int)(255 * (pressed ? 1f : hover)))))
                        g.FillRectangle(b, rect);
                    glyphColor = Theme.Lerp(glyphColor, Color.White, pressed ? 1f : hover);
                }
            }
            else if (hover > 0.01f || pressed)
            {
                int alpha = pressed ? 14 : (int)(26 * hover);
                using (var b = new SolidBrush(Color.FromArgb(alpha, 255, 255, 255)))
                    g.FillRectangle(b, rect);
                if (pressed) glyphColor = Theme.WithAlpha(glyphColor, 190);
            }

            Theme.DrawGlyph(g, glyph, GlyphFont, glyphColor, rect);
        }
    }
}
