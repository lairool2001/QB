using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickBrowser
{
    public class FlatComboBox : ComboBox
    {
        // ── 主題色 ──────────────────────────────────────────
        static readonly Color BgColor    = Color.FromArgb(42, 42, 60);
        static readonly Color BgHover    = Color.FromArgb(55, 55, 78);
        static readonly Color BgSelected = Color.FromArgb(65, 65, 95);
        static readonly Color TextColor  = Color.FromArgb(210, 210, 235);
        static readonly Color ArrowColor = Color.FromArgb(150, 150, 190);
        // ────────────────────────────────────────────────────

        const int GWL_STYLE        = -16;
        const int GWL_EXSTYLE      = -20;
        const int WS_BORDER        = 0x00800000;
        const int WS_EX_CLIENTEDGE = 0x00000200;
        const uint SWP_FLAGS       = 0x0037;

        const int WM_NCPAINT    = 0x0085;
        const int WM_ERASEBKGND = 0x0014;
        const int WM_PAINT      = 0x000F;

        [StructLayout(LayoutKind.Sequential)]
        struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool   fErase;
            public int    rcPaint_left, rcPaint_top, rcPaint_right, rcPaint_bottom;
            public bool   fRestore, fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [DllImport("user32.dll")] static extern int    GetWindowLong(IntPtr h, int n);
        [DllImport("user32.dll")] static extern int    SetWindowLong(IntPtr h, int n, int v);
        [DllImport("user32.dll")] static extern bool   SetWindowPos(IntPtr h, IntPtr a, int x, int y, int cx, int cy, uint f);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern bool   EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);

        private bool _hover;

        public FlatComboBox()
        {
            DrawMode      = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle     = FlatStyle.Flat;
            BackColor     = BgColor;
            ForeColor     = TextColor;
            ItemHeight    = 18;

            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint, true);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SetWindowLong(Handle, GWL_STYLE,   GetWindowLong(Handle, GWL_STYLE)   & ~WS_BORDER);
            SetWindowLong(Handle, GWL_EXSTYLE, GetWindowLong(Handle, GWL_EXSTYLE) & ~WS_EX_CLIENTEDGE);
            SetWindowPos(Handle, IntPtr.Zero, 0, 0, 0, 0, SWP_FLAGS);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_NCPAINT:
                    return;

                case WM_ERASEBKGND:
                    m.Result = (IntPtr)1;   // 告知已處理，OS 不清底
                    return;

                case WM_PAINT:
                    // 完全自己處理，不呼叫 base → 消除雙重繪製閃爍
                    var ps  = new PAINTSTRUCT();
                    var hdc = BeginPaint(Handle, ref ps);
                    using (var g = Graphics.FromHdc(hdc))
                        DrawFace(g);
                    EndPaint(Handle, ref ps);
                    return;
            }

            base.WndProc(ref m);
        }

        private void DrawFace(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 背景（含邊緣 +1 覆蓋殘留線）
            using (var b = new SolidBrush(_hover ? BgHover : BgColor))
                g.FillRectangle(b, new Rectangle(-1, -1, Width + 2, Height + 2));

            // 文字
            if (SelectedIndex >= 0)
                TextRenderer.DrawText(g, Items[SelectedIndex].ToString(), Font,
                    new Rectangle(6, 0, Width - 22, Height), TextColor,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);

            // 三角箭頭
            float cx = Width - 12f, cy = Height / 2f;
            using (var b = new SolidBrush(ArrowColor))
                g.FillPolygon(b, new PointF[]
                {
                    new PointF(cx - 4f, cy - 2f),
                    new PointF(cx + 4f, cy - 2f),
                    new PointF(cx,      cy + 3f)
                });
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? BgSelected : BgColor))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Items[e.Index].ToString(), e.Font,
                new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height),
                TextColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
            Update();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            Invalidate();
            Update();
        }
    }
}
