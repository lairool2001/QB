using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickBrowser
{
    public class FlatNumericUpDown : NumericUpDown
    {
        // ── 主題色（與 FlatComboBox 一致）──────────────────
        static readonly Color BgColor    = Color.FromArgb(42, 42, 60);
        static readonly Color BgHover    = Color.FromArgb(55, 55, 78);
        static readonly Color TextColor  = Color.FromArgb(210, 210, 235);
        static readonly Color ArrowColor = Color.FromArgb(150, 150, 190);
        // ────────────────────────────────────────────────────

        const int GWL_STYLE        = -16;
        const int GWL_EXSTYLE      = -20;
        const int WS_BORDER        = 0x00800000;
        const int WS_EX_CLIENTEDGE = 0x00000200;
        const uint SWP_FLAGS       = 0x0037;
        const int WM_NCPAINT       = 0x0085;

        [DllImport("user32.dll")] static extern int  GetWindowLong(IntPtr h, int n);
        [DllImport("user32.dll")] static extern int  SetWindowLong(IntPtr h, int n, int v);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);

        private SpinPainter _spinPainter;

        public FlatNumericUpDown()
        {
            BackColor   = BgColor;
            ForeColor   = TextColor;
            BorderStyle = BorderStyle.None;   // 移除 TextBox 邊框
        }

        // ── 主控件：移除外框 ────────────────────────────────
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetWindowLong(Handle, GWL_STYLE,   GetWindowLong(Handle, GWL_STYLE)   & ~WS_BORDER);
            SetWindowLong(Handle, GWL_EXSTYLE, GetWindowLong(Handle, GWL_EXSTYLE) & ~WS_EX_CLIENTEDGE);
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_FLAGS);

            // 找到內部的 UpDownButtons 子控件並接管繪製
            foreach (Control c in Controls)
            {
                if (c.GetType().Name == "UpDownButtons")
                {
                    _spinPainter?.ReleaseHandle();
                    _spinPainter = new SpinPainter(c);
                    break;
                }
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCPAINT) return;
            base.WndProc(ref m);
        }

        // ── 接管 UpDownButtons 的繪製 ───────────────────────
        sealed class SpinPainter : NativeWindow
        {
            readonly Control _ctrl;

            const int WM_PAINT      = 0x000F;
            const int WM_ERASEBKGND = 0x0014;

            [StructLayout(LayoutKind.Sequential)]
            struct PAINTSTRUCT
            {
                public IntPtr hdc;
                public bool   fErase;
                public int    l, t, r, b;
                public bool   fRestore, fIncUpdate;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
                public byte[] reserved;
            }

            [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, ref PAINTSTRUCT ps);
            [DllImport("user32.dll")] static extern bool   EndPaint(IntPtr h, ref PAINTSTRUCT ps);

            public SpinPainter(Control ctrl)
            {
                _ctrl = ctrl;
                AssignHandle(ctrl.Handle);
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case WM_ERASEBKGND:
                        m.Result = (IntPtr)1;
                        return;

                    case WM_PAINT:
                        var ps  = new PAINTSTRUCT();
                        var hdc = BeginPaint(Handle, ref ps);
                        using (var g = Graphics.FromHdc(hdc))
                            Draw(g);
                        EndPaint(Handle, ref ps);
                        return;
                }
                base.WndProc(ref m);
            }

            void Draw(Graphics g)
            {
                int w    = _ctrl.Width;
                int h    = _ctrl.Height;
                int half = h / 2;

                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 背景
                using (var b = new SolidBrush(BgColor))
                    g.FillRectangle(b, 0, 0, w, h);

                // 分隔線（上下區段之間）
                using (var p = new Pen(Color.FromArgb(60, 60, 85)))
                    g.DrawLine(p, 0, half, w, half);

                // ▲ 上箭頭
                DrawArrow(g, w / 2f, half / 2f, up: true);

                // ▼ 下箭頭
                DrawArrow(g, w / 2f, half + (h - half) / 2f, up: false);
            }

            static void DrawArrow(Graphics g, float cx, float cy, bool up)
            {
                const float hw = 3.5f, hh = 2.5f;
                PointF[] pts = up
                    ? new[] { new PointF(cx,      cy - hh),
                               new PointF(cx - hw, cy + hh),
                               new PointF(cx + hw, cy + hh) }
                    : new[] { new PointF(cx - hw, cy - hh),
                               new PointF(cx + hw, cy - hh),
                               new PointF(cx,      cy + hh) };

                var b = new SolidBrush(ArrowColor);
                g.FillPolygon(b, pts);
            }
        }
    }
}
