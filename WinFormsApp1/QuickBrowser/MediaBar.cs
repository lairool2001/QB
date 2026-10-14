using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuickBrowser
{
    /// <summary>影片播放時浮在底部的控制列：深色漸層底 + 頂部細線 + 音量圖示。</summary>
    public class MediaBar : Panel
    {
        public MediaBar()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.TitleBar;
        }

        /// <summary>音量滑桿；圖示畫在它左邊。</summary>
        public Control VolumeControl { get; set; }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var b = new LinearGradientBrush(ClientRectangle.IsEmpty ? new Rectangle(0, 0, 1, 1) : ClientRectangle,
                       Theme.Lerp(Theme.TitleBar, Theme.Surface, 0.6f), Theme.TitleBar, LinearGradientMode.Vertical))
                g.FillRectangle(b, ClientRectangle);
            using (var p = new Pen(Theme.Divider))
                g.DrawLine(p, 0, 0, Width, 0);

            if (VolumeControl != null && VolumeControl.Parent == this)
            {
                var r = new RectangleF(VolumeControl.Left - 26, VolumeControl.Top, 22, VolumeControl.Height);
                Theme.DrawGlyph(g, Theme.Glyph.Volume, Theme.IconFont, Theme.TextSecondary, r);
            }
        }
    }
}
