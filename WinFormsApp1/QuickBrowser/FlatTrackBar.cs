using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickBrowser
{
    /// <summary>
    /// 原生 TrackBar 的滑鼠行為不變，只接管繪製：細軌道 + 已播放進度 + 圓形滑塊。
    /// 位置取自原生的 channel / thumb 矩形，所以點擊位置與畫面完全對齊。
    /// </summary>
    public class FlatTrackBar : TrackBar
    {
        const int WM_PAINT = 0x000F;
        const int WM_ERASEBKGND = 0x0014;
        const int WM_PRINTCLIENT = 0x0318;
        const int WM_MOUSEMOVE = 0x0200;
        const int WM_LBUTTONDOWN = 0x0201;
        const int WM_LBUTTONUP = 0x0202;
        const int WM_LBUTTONDBLCLK = 0x0203;
        const int WM_CAPTURECHANGED = 0x0215;
        const int TBM_GETTHUMBRECT = 0x0400 + 25;
        const int TBM_GETCHANNELRECT = 0x0400 + 26;

        [StructLayout(LayoutKind.Sequential)]
        struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore, fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h, int msg, IntPtr w, ref RECT l);
        [DllImport("user32.dll")] static extern IntPtr BeginPaint(IntPtr h, ref PAINTSTRUCT ps);
        [DllImport("user32.dll")] static extern bool EndPaint(IntPtr h, ref PAINTSTRUCT ps);

        readonly Tween _hover;
        bool _dragging;

        public FlatTrackBar()
        {
            TickStyle = TickStyle.None;
            _hover = new Tween(Invalidate, 0.28f);
        }

        /// <summary>軌道顏色以外的強調色，預設為主題強調色。</summary>
        public Color FillColor { get; set; } = Theme.Accent;

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (!_dragging) _hover.To(0f); }

        // ── 滑鼠：自行處理拖曳 ───────────────────────────────
        // 原生 TrackBar 只在「它自己的」滑塊上才開始拖曳，點在軌道上只會跳一個 LargeChange；
        // 自繪的滑塊位置與原生滑塊略有落差，所以改為整條軌道都可直接點擊/拖曳，
        // 位置換算與繪製用同一套幾何。MouseDown/MouseMove/MouseUp/Scroll 事件照常觸發。

        void TrackGeometry(out float x0, out float x1)
        {
            var channel = new RECT();
            SendMessage(Handle, TBM_GETCHANNELRECT, IntPtr.Zero, ref channel);
            x0 = channel.Left + 2;
            x1 = channel.Right - 2;
            if (x1 <= x0) { x0 = 8; x1 = Math.Max(9, Width - 8); }
        }

        void SetValueFromX(int x)
        {
            TrackGeometry(out float x0, out float x1);
            float ratio = Math.Max(0f, Math.Min(1f, (x - x0) / (x1 - x0)));
            int v = Minimum + (int)Math.Round(ratio * (Maximum - Minimum));
            if (v != Value)
            {
                Value = v;
                OnScroll(EventArgs.Empty);
            }
        }

        static MouseEventArgs MouseArgs(Message m, MouseButtons button)
        {
            long lp = m.LParam.ToInt64();
            return new MouseEventArgs(button, 1, (short)(lp & 0xFFFF), (short)((lp >> 16) & 0xFFFF), 0);
        }

        void EndDrag()
        {
            if (!_dragging) return;
            _dragging = false;
            if (!ClientRectangle.Contains(PointToClient(MousePosition))) _hover.To(0f);
            Invalidate();
        }

        protected override void OnValueChanged(EventArgs e) { base.OnValueChanged(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hover.Dispose();
            base.Dispose(disposing);
        }

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_LBUTTONDOWN:
                case WM_LBUTTONDBLCLK:
                {
                    var e = MouseArgs(m, MouseButtons.Left);
                    _dragging = true;
                    Capture = true;
                    if (CanFocus) Focus();
                    SetValueFromX(e.X);
                    Invalidate();
                    OnMouseDown(e);
                    return;
                }
                case WM_MOUSEMOVE:
                    if (_dragging)
                    {
                        var e = MouseArgs(m, MouseButtons.Left);
                        SetValueFromX(e.X);
                        OnMouseMove(e);
                        return;
                    }
                    break;
                case WM_LBUTTONUP:
                    if (_dragging)
                    {
                        var e = MouseArgs(m, MouseButtons.Left);
                        SetValueFromX(e.X);
                        Capture = false;
                        EndDrag();
                        OnMouseUp(e);
                        return;
                    }
                    break;
                case WM_CAPTURECHANGED:
                    EndDrag();
                    break;
                case WM_ERASEBKGND:
                    m.Result = (IntPtr)1;
                    return;
                case WM_PRINTCLIENT:
                    using (var g = Graphics.FromHdc(m.WParam))
                        Render(g);
                    return;
                case WM_PAINT:
                    if (m.WParam != IntPtr.Zero)
                    {
                        using (var g = Graphics.FromHdc(m.WParam))
                            Render(g);
                        return;
                    }
                    var ps = new PAINTSTRUCT();
                    var hdc = BeginPaint(Handle, ref ps);
                    using (var buffer = BufferedGraphicsManager.Current.Allocate(hdc, ClientRectangle))
                    {
                        Render(buffer.Graphics);
                        buffer.Render();
                    }
                    EndPaint(Handle, ref ps);
                    return;
            }
            base.WndProc(ref m);
        }

        void Render(Graphics g)
        {
            // 背景：沿用父容器底色（讓滑桿「浮」在面板上）
            if (Parent != null)
            {
                var state = g.Save();
                g.TranslateTransform(-Left, -Top);
                InvokePaintBackground(Parent, new PaintEventArgs(g, Bounds));
                g.Restore(state);
            }
            else
            {
                g.Clear(BackColor);
            }

            Theme.HighQuality(g);

            var channel = new RECT();
            var thumb = new RECT();
            SendMessage(Handle, TBM_GETCHANNELRECT, IntPtr.Zero, ref channel);
            SendMessage(Handle, TBM_GETTHUMBRECT, IntPtr.Zero, ref thumb);

            float h = _hover.Value;
            float cy = (thumb.Top + thumb.Bottom) / 2f;
            float x0 = channel.Left + 2, x1 = channel.Right - 2;
            float tx = (thumb.Left + thumb.Right) / 2f;
            if (Maximum > Minimum && x1 > x0)
                tx = x0 + (x1 - x0) * (Value - Minimum) / (float)(Maximum - Minimum);

            float trackH = 4f + h * 2f;
            var track = new RectangleF(x0, cy - trackH / 2f, x1 - x0, trackH);
            Theme.FillRound(g, Enabled ? Color.FromArgb(56, 56, 82) : Theme.Divider, track, trackH / 2f);

            if (tx > x0 + 0.5f)
            {
                var filled = new RectangleF(x0, track.Y, tx - x0, trackH);
                var fill = Enabled ? FillColor : Theme.TextDisabled;
                using (var path = Theme.RoundRect(filled, trackH / 2f))
                using (var b = new LinearGradientBrush(new RectangleF(x0 - 1, track.Y, Math.Max(2f, tx - x0 + 2), trackH),
                           Theme.Lerp(fill, Theme.Base, 0.25f), fill, LinearGradientMode.Horizontal))
                    g.FillPath(b, path);
            }

            // 滑塊：hover 時放大並出現光暈
            float r = 5.5f + h * 2f + (_dragging ? 0.5f : 0f);
            if (h > 0.01f && Enabled)
            {
                float glow = r + 5f * h;
                using (var b = new SolidBrush(Theme.WithAlpha(FillColor, (int)(55 * h))))
                    g.FillEllipse(b, tx - glow, cy - glow, glow * 2, glow * 2);
            }
            using (var b = new SolidBrush(Enabled ? Color.White : Theme.TextMuted))
                g.FillEllipse(b, tx - r, cy - r, r * 2, r * 2);
            using (var p = new Pen(Theme.WithAlpha(Enabled ? FillColor : Theme.Divider, 220), 1.5f))
                g.DrawEllipse(p, tx - r, cy - r, r * 2, r * 2);
        }
    }

    /// <summary>
    /// 開關樣式的 CheckBox（膠囊 + 滑動圓點），文字置於右側。
    /// </summary>
    public class ToggleSwitch : CheckBox
    {
        readonly Tween _on;
        readonly Tween _hover;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint, true);
            AutoSize = false;
            Cursor = Cursors.Hand;
            Font = Theme.UiFont;
            _on = new Tween(Invalidate, 0.25f);
            _hover = new Tween(Invalidate);
        }

        protected override void OnCheckedChanged(EventArgs e) { base.OnCheckedChanged(e); _on.To(Checked ? 1f : 0f); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover.To(0f); }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            _on.Snap(Checked ? 1f : 0f);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _on.Dispose(); _hover.Dispose(); }
            base.Dispose(disposing);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent) { }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            if (Parent != null)
            {
                var state = g.Save();
                g.TranslateTransform(-Left, -Top);
                InvokePaintBackground(Parent, new PaintEventArgs(g, Bounds));
                g.Restore(state);
            }
            Theme.HighQuality(g);

            float t = _on.Value, h = _hover.Value;
            const float w = 32f, hh = 18f;
            var pill = new RectangleF(1, (Height - hh) / 2f, w, hh);

            var off = Theme.Lerp(Theme.Control, Theme.ControlHover, h);
            var on = Theme.Lerp(Theme.Accent, Theme.AccentHover, h);
            Theme.FillRound(g, Theme.Lerp(off, on, t), pill, hh / 2f);
            if (t < 0.99f)
                Theme.DrawRound(g, Theme.WithAlpha(Theme.ControlBorder, (int)(255 * (1 - t))), pill, hh / 2f);

            float knob = 12f + h * 1f;
            float kx = pill.X + 3f + (w - 6f - knob) * t;
            float ky = pill.Y + (hh - knob) / 2f;
            using (var b = new SolidBrush(Theme.Lerp(Theme.TextSecondary, Color.White, t)))
                g.FillEllipse(b, kx, ky, knob, knob);

            var textRect = new Rectangle((int)(pill.Right + 8), 0, Width - (int)pill.Right - 8, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Theme.Lerp(Theme.TextSecondary, Theme.Text, Math.Max(h, t)),
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }
}
