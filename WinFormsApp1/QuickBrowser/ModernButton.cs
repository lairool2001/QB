using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace QuickBrowser
{
    public enum ButtonKind
    {
        Default,
        Accent,
        Danger,
        Success,
        Info,
        Ghost,
    }

    /// <summary>
    /// 圓角、圖示 + 文字、hover 漸變的扁平按鈕。保留 Button 的所有事件，可直接替換。
    /// </summary>
    public class ModernButton : Button
    {
        readonly Tween _hover;
        bool _pressed;
        ButtonKind _kind;
        string _glyph = "";
        string _hint = "";

        public ModernButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw |
                     ControlStyles.UserPaint |
                     ControlStyles.SupportsTransparentBackColor, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Font = Theme.UiFont;
            Cursor = Cursors.Hand;
            _hover = new Tween(Invalidate);
        }

        [DefaultValue(ButtonKind.Default)]
        public ButtonKind Kind
        {
            get => _kind;
            set { _kind = value; Invalidate(); }
        }

        /// <summary>Segoe Fluent Icons 字元，例如 Theme.Glyph.Home。</summary>
        [DefaultValue("")]
        public string Glyph
        {
            get => _glyph;
            set { _glyph = value ?? ""; Invalidate(); }
        }

        /// <summary>靠右顯示的淡色提示（例如快捷鍵）。</summary>
        [DefaultValue("")]
        public string Hint
        {
            get => _hint;
            set { _hint = value ?? ""; Invalidate(); }
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hover.To(1f); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hover.To(0f); _pressed = false; }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = true; Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _pressed = false;
            Invalidate();
            base.OnMouseUp(e);
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (!Enabled) _hover.Snap(0f);
            Invalidate();
        }

        protected override void OnTextChanged(EventArgs e) { base.OnTextChanged(e); Invalidate(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _hover.Dispose();
            base.Dispose(disposing);
        }

        void Palette(out Color bg, out Color border, out Color fg, out Color glyph)
        {
            float h = _hover.Value;
            Color rest, hot, accent;
            switch (_kind)
            {
                case ButtonKind.Accent:
                    rest = Theme.Accent; hot = Theme.AccentHover;
                    bg = Theme.Lerp(rest, hot, h);
                    border = Theme.WithAlpha(Color.White, 28);
                    fg = glyph = Color.White;
                    break;
                case ButtonKind.Danger:
                    // 破壞性操作平時低調，hover 時才轉為實心紅，降低誤觸
                    accent = Theme.Danger;
                    bg = Theme.Lerp(Theme.Lerp(Theme.Control, accent, 0.14f), accent, h);
                    border = Theme.Lerp(Theme.Lerp(Theme.ControlBorder, accent, 0.35f), accent, h);
                    fg = Theme.Lerp(Theme.Lerp(Theme.Text, accent, 0.35f), Color.White, h);
                    glyph = Theme.Lerp(accent, Color.White, h);
                    break;
                case ButtonKind.Success:
                case ButtonKind.Info:
                    accent = _kind == ButtonKind.Success ? Theme.Success : Theme.Info;
                    bg = Theme.Lerp(Theme.Lerp(Theme.Control, accent, 0.12f), Theme.Lerp(Theme.Control, accent, 0.26f), h);
                    border = Theme.Lerp(Theme.Lerp(Theme.ControlBorder, accent, 0.30f), Theme.Lerp(Theme.ControlBorder, accent, 0.6f), h);
                    fg = Theme.Text;
                    glyph = accent;
                    break;
                case ButtonKind.Ghost:
                    bg = Theme.WithAlpha(Theme.ControlHover, (int)(255 * h));
                    border = Color.Transparent;
                    fg = Theme.Lerp(Theme.TextSecondary, Theme.Text, h);
                    glyph = fg;
                    break;
                default:
                    bg = Theme.Lerp(Theme.Control, Theme.ControlHover, h);
                    border = Theme.Lerp(Theme.ControlBorder, Theme.Lerp(Theme.ControlBorder, Theme.Accent, 0.45f), h);
                    fg = Theme.Text;
                    glyph = Theme.Lerp(Theme.TextSecondary, Theme.Text, h);
                    break;
            }

            if (_pressed)
            {
                bg = Theme.Lerp(bg, Color.Black, 0.18f);
            }

            if (!Enabled)
            {
                bg = Theme.Lerp(Theme.Control, Theme.Base, 0.45f);
                border = Theme.Divider;
                fg = glyph = Theme.TextDisabled;
            }
        }

        void PaintParentBackground(Graphics g)
        {
            if (Parent == null) return;
            var state = g.Save();
            g.TranslateTransform(-Left, -Top);
            var pe = new PaintEventArgs(g, Bounds);
            InvokePaintBackground(Parent, pe);
            g.Restore(state);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            // 全部在 OnPaint 處理
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            // 讓圓角外露出父容器（含側欄卡片）背景
            PaintParentBackground(g);
            Theme.HighQuality(g);

            Palette(out var bg, out var border, out var fg, out var glyphColor);

            var rect = new RectangleF(0, 0, Width, Height);
            if (_pressed) rect.Inflate(-0.5f, -0.5f);
            Theme.FillRound(g, bg, rect, Theme.Radius);
            if (border.A > 0) Theme.DrawRound(g, border, rect, Theme.Radius);

            // 內容：[圖示] [文字] ........ [提示]
            const int pad = 8, gap = 6, glyphW = 16;
            bool hasGlyph = _glyph.Length > 0;
            bool hasText = !string.IsNullOrEmpty(Text);
            // 量測不可帶 EndEllipsis（搭配 Size.Empty 會回傳錯誤寬度）
            var measure = TextFormatFlags.NoPadding | TextFormatFlags.SingleLine;
            var flags = measure | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;

            int hintW = 0;
            if (_hint.Length > 0)
            {
                hintW = TextRenderer.MeasureText(g, _hint, Theme.CaptionFont, Size.Empty, measure).Width;
                var hintRect = new Rectangle(Width - pad - hintW, 0, hintW, Height);
                var hintColor = Enabled ? Theme.WithAlpha(fg, 130) : Theme.TextDisabled;
                TextRenderer.DrawText(g, _hint, Theme.CaptionFont, hintRect, hintColor, flags);
                hintW += gap;
            }

            int textW = hasText ? TextRenderer.MeasureText(g, Text, Font, Size.Empty, measure).Width : 0;
            int avail = Width - pad * 2 - hintW;
            int contentW = (hasGlyph ? glyphW : 0) + (hasGlyph && hasText ? gap : 0) + Math.Min(textW, avail);
            int x = hintW > 0 ? pad : Math.Max(pad, (Width - contentW) / 2);
            if (!hasText) x = (Width - glyphW) / 2;

            if (hasGlyph)
            {
                Theme.DrawGlyph(g, _glyph, Theme.IconFont, glyphColor, new RectangleF(x, 0, glyphW, Height));
                x += glyphW + gap;
            }

            if (hasText)
            {
                var textRect = new Rectangle(x, 0, Math.Max(0, Width - x - pad - hintW + (hintW > 0 ? 0 : pad)), Height);
                TextRenderer.DrawText(g, Text, Font, textRect, fg, flags);
            }

            if (Focused && ShowFocusCues)
            {
                var focus = rect;
                focus.Inflate(-2, -2);
                Theme.DrawRound(g, Theme.WithAlpha(Theme.Accent, 200), focus, Theme.Radius - 2);
            }
        }
    }
}
