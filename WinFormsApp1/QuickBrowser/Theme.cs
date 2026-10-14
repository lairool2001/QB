using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace QuickBrowser
{
    /// <summary>
    /// 全域設計語彙：色彩、字型、圓角與動畫工具。所有自繪控制項都從這裡取值，確保風格一致。
    /// </summary>
    public static class Theme
    {
        // ── 底層 ─────────────────────────────────────────────
        public static readonly Color Base          = Color.FromArgb(22, 22, 35);    // 視窗底色
        public static readonly Color TitleBar      = Color.FromArgb(18, 18, 29);    // 標題列
        public static readonly Color Surface       = Color.FromArgb(29, 29, 45);    // 側欄卡片
        public static readonly Color SurfaceBorder = Color.FromArgb(40, 40, 60);
        public static readonly Color Divider       = Color.FromArgb(36, 36, 54);

        // ── 輸入欄位 ─────────────────────────────────────────
        public static readonly Color Field         = Color.FromArgb(37, 37, 56);
        public static readonly Color FieldBorder   = Color.FromArgb(52, 52, 76);
        public static readonly Color FieldReadOnly = Color.FromArgb(33, 33, 50);

        // ── 按鈕 ─────────────────────────────────────────────
        public static readonly Color Control        = Color.FromArgb(44, 44, 66);
        public static readonly Color ControlHover   = Color.FromArgb(56, 56, 84);
        public static readonly Color ControlPressed = Color.FromArgb(38, 38, 57);
        public static readonly Color ControlBorder  = Color.FromArgb(62, 62, 90);

        // ── 文字 ─────────────────────────────────────────────
        public static readonly Color Text          = Color.FromArgb(226, 226, 242);
        public static readonly Color TextSecondary = Color.FromArgb(160, 160, 192);
        public static readonly Color TextMuted     = Color.FromArgb(112, 112, 146);
        public static readonly Color TextDisabled  = Color.FromArgb(84, 84, 110);

        // ── 語意色 ───────────────────────────────────────────
        public static readonly Color Accent        = Color.FromArgb(124, 108, 255);
        public static readonly Color AccentHover   = Color.FromArgb(146, 133, 255);
        public static readonly Color Danger        = Color.FromArgb(232, 76, 92);
        public static readonly Color Success       = Color.FromArgb(56, 196, 146);
        public static readonly Color Info          = Color.FromArgb(78, 168, 236);
        public static readonly Color Warning       = Color.FromArgb(242, 184, 64);
        public static readonly Color CloseHover    = Color.FromArgb(196, 43, 28);   // Windows 11 關閉紅

        // ── 操作模式（richTextBox2 指示）─────────────────────
        public static readonly Color ModeGo     = Field;
        public static readonly Color ModeDelete = Color.FromArgb(92, 34, 46);
        public static readonly Color ModeSelect = Color.FromArgb(88, 70, 28);

        // ── 尺寸 ─────────────────────────────────────────────
        public const int TitleBarHeight = 36;
        public const int Radius         = 6;
        public const int CardRadius     = 10;

        // ── 字型 ─────────────────────────────────────────────
        public static readonly Font UiFont       = CreateFont(9f, FontStyle.Regular, "Microsoft JhengHei UI", "微軟正黑體", "Segoe UI");
        public static readonly Font UiFontBold   = CreateFont(9f, FontStyle.Bold, "Microsoft JhengHei UI", "微軟正黑體", "Segoe UI");
        public static readonly Font CaptionFont  = CreateFont(7.5f, FontStyle.Bold, "Segoe UI", "Microsoft JhengHei UI");
        public static readonly Font TitleFont    = CreateFont(9f, FontStyle.Bold, "Segoe UI Variable Text", "Segoe UI", "Microsoft JhengHei UI");
        public static readonly Font StatusFont   = CreateFont(9f, FontStyle.Regular, "Microsoft JhengHei UI", "Segoe UI");
        public static readonly Font IconFont     = CreateFont(10f, FontStyle.Regular, "Segoe Fluent Icons", "Segoe MDL2 Assets");
        public static readonly Font IconFontSmall = CreateFont(8f, FontStyle.Regular, "Segoe Fluent Icons", "Segoe MDL2 Assets");

        // Segoe Fluent Icons / MDL2 共用碼位
        public static class Glyph
        {
            public const string Back       = "\uE72B";
            public const string Forward    = "\uE72A";
            public const string History    = "\uE81C";
            public const string Redo       = "\uE7A6";
            public const string Copy       = "\uE8C8";
            public const string Delete     = "\uE74D";
            public const string Home       = "\uE80F";
            public const string List       = "\uE8FD";
            public const string Refresh    = "\uE72C";
            public const string Folder     = "\uE8B7";
            public const string OpenFolder = "\uED25";
            public const string Move       = "\uE8DE";
            public const string Drag       = "\uE7C2";
            public const string Command    = "\uE756";
            public const string Unzip      = "\uE7B8";
            public const string Play       = "\uE768";
            public const string Pause      = "\uE769";
            public const string Stop       = "\uE71A";
            public const string Resume     = "\uE768";
            public const string Reverse    = "\uE892";
            public const string Volume     = "\uE767";
            public const string Minimize   = "\uE921";
            public const string Maximize   = "\uE922";
            public const string Restore    = "\uE923";
            public const string Close      = "\uE8BB";
            public const string Rename     = "\uE8AC";
            public const string Info       = "\uE946";
            public const string Sort       = "\uE8CB";
            public const string Display    = "\uE740";
            public const string Picture    = "\uE8B9";
            public const string Filter     = "\uE71C";
        }

        static Font CreateFont(float size, FontStyle style, params string[] families)
        {
            using (var installed = new InstalledFontCollection())
            {
                foreach (var name in families)
                    foreach (var f in installed.Families)
                        if (string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase))
                            return new Font(f.Name, size, style, GraphicsUnit.Point);
            }
            return new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Point);
        }

        // ── 繪圖工具 ─────────────────────────────────────────
        public static GraphicsPath RoundRect(RectangleF r, float radius)
        {
            var path = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Color color, RectangleF r, float radius)
        {
            using (var path = RoundRect(r, radius))
            using (var b = new SolidBrush(color))
                g.FillPath(b, path);
        }

        public static void DrawRound(Graphics g, Color color, RectangleF r, float radius, float width = 1f)
        {
            // 半像素內縮讓 1px 線條落在像素格上，避免糊邊
            var rr = new RectangleF(r.X + 0.5f, r.Y + 0.5f, r.Width - 1f, r.Height - 1f);
            using (var path = RoundRect(rr, radius))
            using (var p = new Pen(color, width))
                g.DrawPath(p, path);
        }

        public static Color Lerp(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color WithAlpha(Color c, int alpha) => Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c);

        public static void HighQuality(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
        }

        public static void DrawGlyph(Graphics g, string glyph, Font font, Color color, RectangleF bounds)
        {
            using (var sf = new StringFormat(StringFormat.GenericTypographic)
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.NoClip,
            })
            using (var b = new SolidBrush(color))
            {
                var old = g.TextRenderingHint;
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                g.DrawString(glyph, font, b, bounds, sf);
                g.TextRenderingHint = old;
            }
        }

        /// <summary>與主題一致的深色工具提示。</summary>
        public static ToolTip CreateToolTip(IContainer container = null)
        {
            var tip = container == null ? new ToolTip() : new ToolTip(container);
            tip.OwnerDraw = true;
            tip.InitialDelay = 450;
            tip.ReshowDelay = 120;
            tip.AutoPopDelay = 8000;
            tip.BackColor = Color.FromArgb(40, 40, 60);
            tip.ForeColor = Text;
            tip.Popup += (s, e) =>
            {
                var size = TextRenderer.MeasureText(tip.GetToolTip(e.AssociatedControl), UiFont);
                e.ToolTipSize = new Size(size.Width + 16, size.Height + 10);
            };
            tip.Draw += (s, e) =>
            {
                var g = e.Graphics;
                using (var b = new SolidBrush(Color.FromArgb(40, 40, 60)))
                    g.FillRectangle(b, e.Bounds);
                using (var p = new Pen(ControlBorder))
                    g.DrawRectangle(p, 0, 0, e.Bounds.Width - 1, e.Bounds.Height - 1);
                TextRenderer.DrawText(g, e.ToolTipText, UiFont, e.Bounds, Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            };
            return tip;
        }
    }

    /// <summary>
    /// 輕量補間：以 60fps 的 UI 計時器把 Value 平滑推向 Target，供 hover / press 動畫使用。
    /// </summary>
    internal sealed class Tween : IDisposable
    {
        readonly Timer _timer = new Timer { Interval = 15 };
        readonly Action _onStep;
        readonly float _speed;
        public float Value { get; private set; }
        public float Target { get; private set; }

        public Tween(Action onStep, float speed = 0.22f)
        {
            _onStep = onStep;
            _speed = speed;
            _timer.Tick += (s, e) =>
            {
                Value += (Target - Value) * _speed;
                if (Math.Abs(Target - Value) < 0.01f)
                {
                    Value = Target;
                    _timer.Stop();
                }
                _onStep();
            };
        }

        public void To(float target)
        {
            Target = target;
            if (Math.Abs(Target - Value) >= 0.01f) _timer.Start();
        }

        public void Snap(float value)
        {
            Target = Value = value;
            _timer.Stop();
        }

        public void Dispose() => _timer.Dispose();
    }
}
