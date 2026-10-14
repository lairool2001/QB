using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace QuickBrowser
{
    /// <summary>
    /// Form1 的外觀層：自訂標題列、側欄卡片、輸入欄位外框與工具提示。
    /// 只負責「畫」與掛事件，不碰任何瀏覽邏輯。
    /// </summary>
    public partial class Form1
    {
        WindowChrome chrome;
        // 拖曳縮放時約 30ms 同步一次，避免每個 SizeChanged 都重建點陣圖
        readonly Timer liveResizeTimer = new Timer { Interval = 30 };

        /// <summary>內容區頂端（標題列下緣）；無邊框全螢幕時為 0。</summary>
        int chromeTop => chrome?.ContentTop ?? 0;

        /// <summary>畫在表單底上的輸入欄位：圓角底色 + 左側圖示槽 + 焦點框。</summary>
        sealed class FieldSpec
        {
            public Control Control;
            public string Glyph;
            public Padding Pad;
            public Func<Color> Fill;

            public Rectangle Outer => new Rectangle(
                Control.Left - Pad.Left, Control.Top - Pad.Top,
                Control.Width + Pad.Horizontal, Control.Height + Pad.Vertical);
        }

        readonly List<FieldSpec> fields = new List<FieldSpec>();
        Control[] navigationCard, actionsCard;

        void ApplyModernUi()
        {
            chrome = new WindowChrome(this);
            ResizeRedraw = true;
            // 建立 handle 時標題列併入工作區會改變尺寸，但 Load 還沒算出 pictureBoxWidthLess；
            // Load 之前先拔掉 SizeChanged / Resize，否則內容區會被拉到整個視窗寬
            SizeChanged -= Form1_SizeChanged;
            Resize -= Form1_Resize;
            HandleCreated += (s, e) => chrome.CorrectInitialHeight();
            Load += (s, e) =>
            {
                SizeChanged += Form1_SizeChanged;
                Resize += Form1_Resize;
                // 視窗大小改變時即時同步檔案區（原本只在拖曳結束 ResizeEnd 才更新，最大化/還原時甚至不會更新）
                SizeChanged += (s2, e2) => { if (!liveResizeTimer.Enabled) liveResizeTimer.Start(); };
            };
            liveResizeTimer.Tick += (s, e) =>
            {
                liveResizeTimer.Stop();
                syncLayoutAfterResize();
            };

            Func<Color> field = () => Theme.Field;
            AddField(richTextBox1, Theme.Glyph.Folder, new Padding(34, 7, 10, 7), field);
            AddField(richTextBox2, null, new Padding(8, 6, 8, 6), () => richTextBox2.BackColor);
            AddField(richTextBox3, Theme.Glyph.Rename, new Padding(32, 7, 10, 7), field);
            AddField(richTextBox4, Theme.Glyph.Info, new Padding(32, 6, 10, 6), () => Theme.FieldReadOnly);
            AddField(numericUpDown1, null, new Padding(8, 6, 8, 5), field);
            AddField(comboBox2, Theme.Glyph.Sort, new Padding(32, 3, 8, 3), field);
            AddField(richTextBox6, Theme.Glyph.Move, new Padding(32, 7, 10, 7), field);
            AddField(comboBox1, Theme.Glyph.Display, new Padding(32, 3, 8, 3), field);
            AddField(richTextBox7, Theme.Glyph.Picture, new Padding(32, 7, 10, 7), field);
            AddField(richTextBox5, Theme.Glyph.Filter, new Padding(32, 9, 10, 9), field);

            navigationCard = new Control[]
            {
                richTextBox2, button1, button12, button2, button11, richTextBox3, richTextBox4, label2,
                button4, button17, button16, numericUpDown1, button5, comboBox2, button9, button14, button15,
            };
            actionsCard = new Control[]
            {
                button7, button8, button13, richTextBox6, comboBox1, richTextBox7, button6, button10, richTextBox5,
            };

            // 操作模式色會改變欄位底色，需重畫外框
            richTextBox2.BackColorChanged += (s, e) => InvalidateField(richTextBox2);
            // 影片元件第一次顯示時才建立視窗，會被插到影片控制列上方；控制列每次出現都提到最前
            groupBox1.VisibleChanged += (s, e) =>
            {
                if (groupBox1.Visible) groupBox1.BringToFront();
            };
            // 檢視器模式會整批隱藏控制項，卡片要跟著消失/出現
            button9.VisibleChanged += (s, e) => Invalidate();

            var tip = Theme.CreateToolTip(components);
            tip.SetToolTip(richTextBox1, "目前路徑");
            tip.SetToolTip(button3, "複製目前路徑");
            tip.SetToolTip(richTextBox2, "輸入編號跳轉　+ 開新視窗　- 刪除一位　* 搜尋");
            tip.SetToolTip(button1, "上一頁");
            tip.SetToolTip(button12, "下一頁");
            tip.SetToolTip(button2, "較舊的紀錄");
            tip.SetToolTip(button11, "重新較舊的紀錄");
            tip.SetToolTip(richTextBox3, "檔名");
            tip.SetToolTip(richTextBox4, "選取資訊");
            tip.SetToolTip(button4, "複製選取項目的路徑");
            tip.SetToolTip(button17, "執行命令");
            tip.SetToolTip(button16, "解壓縮");
            tip.SetToolTip(button5, "刪除選取項目");
            tip.SetToolTip(comboBox2, "排序方式");
            tip.SetToolTip(button9, "首頁");
            tip.SetToolTip(button14, "清單");
            tip.SetToolTip(button15, "重新整理");
            tip.SetToolTip(button7, "複製到目標資料夾");
            tip.SetToolTip(button8, "移動到目標資料夾");
            tip.SetToolTip(button13, "拖曳選取項目");
            tip.SetToolTip(richTextBox6, "目標資料夾");
            tip.SetToolTip(comboBox1, "圖片顯示方式");
            tip.SetToolTip(richTextBox7, "資料夾背景圖");
            tip.SetToolTip(button6, "只顯示資料夾 (Ctrl+J)");
            tip.SetToolTip(button10, "在檔案總管開啟");
            tip.SetToolTip(richTextBox5, "篩選");
            tip.SetToolTip(trackBar3, "音量");
        }

        void AddField(Control control, string glyph, Padding pad, Func<Color> fill)
        {
            var spec = new FieldSpec { Control = control, Glyph = glyph, Pad = pad, Fill = fill };
            fields.Add(spec);
            control.GotFocus += (s, e) => InvalidateField(control);
            control.LostFocus += (s, e) => InvalidateField(control);
            control.MouseEnter += (s, e) => InvalidateField(control);
            control.MouseLeave += (s, e) => InvalidateField(control);
            control.VisibleChanged += (s, e) => InvalidateField(control);
        }

        void InvalidateField(Control control)
        {
            var spec = fields.FirstOrDefault(f => f.Control == control);
            if (spec == null) return;
            var r = spec.Outer;
            r.Inflate(2, 2);
            Invalidate(r, true);
        }

        // WinForms 以「含系統標題列」換算視窗大小（設定 ClientSize、還原視窗時），扣掉已併入工作區的部分
        protected override Size SizeFromClientSize(Size clientSize)
        {
            var size = base.SizeFromClientSize(clientSize);
            if (chrome != null && chrome.Enabled && IsHandleCreated)
                size.Height -= WindowChrome.SystemTopFrame();
            return size;
        }

        protected override void WndProc(ref Message m)
        {
            if (chrome != null && chrome.PreProcess(ref m)) return;
            base.WndProc(ref m);
            chrome?.PostProcess(ref m);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            base.OnPaintBackground(e);
            if (chrome == null) return;

            var g = e.Graphics;
            Theme.HighQuality(g);
            var clip = e.ClipRectangle;

            // 檔案格線外框
            if (_drawPanel != null && _drawPanel.Visible && _drawPanel.Dock == DockStyle.None)
            {
                var frame = _drawPanel.Bounds;
                frame.Inflate(1, 1);
                if (frame.IntersectsWith(Inflate(clip, 4)))
                    Theme.DrawRound(g, Theme.SurfaceBorder, frame, 4);
            }

            DrawCard(g, clip, navigationCard, "NAVIGATION");
            DrawCard(g, clip, actionsCard, "FILE ACTIONS");

            foreach (var f in fields)
            {
                if (!f.Control.Visible) continue;
                var outer = f.Outer;
                if (!outer.IntersectsWith(Inflate(clip, 2))) continue;

                Theme.FillRound(g, f.Fill(), outer, Theme.Radius);

                bool focused = f.Control.ContainsFocus;
                bool hover = f.Control.ClientRectangle.Contains(f.Control.PointToClient(MousePosition));
                var border = focused ? Theme.Accent
                           : hover ? Theme.Lerp(Theme.FieldBorder, Theme.TextMuted, 0.5f)
                           : Theme.FieldBorder;
                Theme.DrawRound(g, border, outer, Theme.Radius);
                if (focused)
                {
                    // 焦點光暈
                    var glow = outer;
                    glow.Inflate(2, 2);
                    Theme.DrawRound(g, Theme.WithAlpha(Theme.Accent, 60), glow, Theme.Radius + 2, 2f);
                }

                if (f.Glyph != null)
                {
                    // 一般高度置中；較高的多行欄位則對齊第一行
                    float gh = outer.Height <= 48 ? outer.Height : 34;
                    var gutter = new RectangleF(outer.X + 2, outer.Y, f.Pad.Left - 4, gh);
                    Theme.DrawGlyph(g, f.Glyph, Theme.IconFont, focused ? Theme.Accent : Theme.TextMuted, gutter);
                }
            }
        }

        static Rectangle Inflate(Rectangle r, int by)
        {
            r.Inflate(by, by);
            return r;
        }

        // ── 檔案區右側捲軸（在繪圖執行緒上畫進點陣圖）─────────────────
        // 幾何與 hitRightScrollBar / jumpRightScrollBar 一致：中心線 x = blockRight - 20，
        // 軌道 y = 20 ~ bitmapHeight - 20，滑塊中心 = 20 + 進度 × 軌道長。

        volatile bool scrollBarHover;
        float scrollBarHot;                 // 0~1，hover/拖曳時變粗變亮
        float scrollBarAwake = 1f;          // 0~1，閒置時淡出
        float scrollBarLastY = float.NaN;
        readonly Stopwatch scrollBarClock = Stopwatch.StartNew();
        long scrollBarLastActive;
        // 繪圖執行緒專用字型（GDI+ 物件不能跨執行緒同時使用）
        readonly Font scrollBarFont = new Font("Segoe UI", 8f, FontStyle.Bold);

        void drawRightScrollBar(Graphics g, float scrollHeight)
        {
            long now = scrollBarClock.ElapsedMilliseconds;
            bool dragging = leftScrollDown;
            if (scrollY != scrollBarLastY || dragging || scrollBarHover)
            {
                scrollBarLastY = scrollY;
                scrollBarLastActive = now;
            }

            // 每幀朝目標值逼近，繪圖執行緒約 144fps，所以不需要額外計時器
            float hotTarget = dragging ? 1f : scrollBarHover ? 0.8f : 0f;
            scrollBarHot += (hotTarget - scrollBarHot) * 0.2f;
            float awakeTarget = now - scrollBarLastActive < 900 ? 1f : 0f;
            scrollBarAwake += (awakeTarget - scrollBarAwake) * (awakeTarget > scrollBarAwake ? 0.35f : 0.06f);

            var oldSmoothing = g.SmoothingMode;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            float cx = blockRight - 20;
            float top = 20, bottom = bitmapHeight - 20;
            float trackLen = bottom - top;
            float width = 4f + 6f * scrollBarHot;

            // 軌道
            var track = new RectangleF(cx - width / 2f, top - width / 2f, width, trackLen + width);
            int trackAlpha = (int)(26 + 40 * scrollBarHot);
            using (var path = Theme.RoundRect(track, width / 2f))
            using (var b = new SolidBrush(Color.FromArgb(trackAlpha, 255, 255, 255)))
                g.FillPath(b, path);

            // 滑塊長度依「可視比例」，夾在 40 ~ 軌道一半
            float ratio = Math.Max(0f, Math.Min(1f, -scrollY / scrollHeight));
            float visible = bitmapHeight / (scrollHeight + bitmapHeight);
            float thumbLen = Math.Max(40f, Math.Min(trackLen * 0.5f, trackLen * visible));
            float center = top + ratio * trackLen;
            float thumbTop = Math.Max(top - width / 2f, Math.Min(bottom + width / 2f - thumbLen, center - thumbLen / 2f));
            var thumb = new RectangleF(cx - width / 2f, thumbTop, width, thumbLen);

            float alpha = 0.55f + 0.45f * Math.Max(scrollBarAwake, scrollBarHot);
            var c1 = Theme.Lerp(Theme.Accent, Theme.AccentHover, scrollBarHot);
            var c2 = Theme.Lerp(Theme.Lerp(Theme.Accent, Color.White, 0.25f), Color.White, scrollBarHot * 0.4f);

            if (scrollBarHot > 0.05f)
            {
                // 光暈
                var glow = thumb;
                glow.Inflate(4f * scrollBarHot, 4f * scrollBarHot);
                using (var path = Theme.RoundRect(glow, glow.Width / 2f))
                using (var b = new SolidBrush(Color.FromArgb((int)(60 * scrollBarHot), Theme.Accent)))
                    g.FillPath(b, path);
            }
            using (var path = Theme.RoundRect(thumb, width / 2f))
            using (var b = new System.Drawing.Drawing2D.LinearGradientBrush(
                       new RectangleF(thumb.X, thumb.Y - 1, thumb.Width, thumb.Height + 2),
                       Color.FromArgb((int)(255 * alpha), c2), Color.FromArgb((int)(255 * alpha), c1),
                       System.Drawing.Drawing2D.LinearGradientMode.Vertical))
                g.FillPath(b, path);

            // 拖曳時在左側顯示「目前 / 總數」
            if (dragging && cardList != null && cardList.Count > 0)
            {
                int total = cardList.Count;
                int index = Math.Min(total, 1 + (int)(ratio * (total - 1)));
                string label = index + " / " + total;
                var size = g.MeasureString(label, scrollBarFont);
                var bubble = new RectangleF(cx - 14 - size.Width - 16, center - (size.Height + 8) / 2f, size.Width + 16, size.Height + 8);
                bubble.Y = Math.Max(4, Math.Min(bitmapHeight - bubble.Height - 4, bubble.Y));
                using (var path = Theme.RoundRect(bubble, bubble.Height / 2f))
                using (var b = new SolidBrush(Color.FromArgb(235, 30, 30, 46)))
                using (var p = new Pen(Theme.Accent, 1f))
                {
                    g.FillPath(b, path);
                    g.DrawPath(p, path);
                }
                using (var b = new SolidBrush(Theme.Text))
                    g.DrawString(label, scrollBarFont, b, bubble.X + 8, bubble.Y + 4);
            }

            g.SmoothingMode = oldSmoothing;
        }

        void DrawCard(Graphics g, Rectangle clip, Control[] members, string caption)
        {
            if (members == null || !members.Any(c => c.Visible)) return;

            Rectangle bounds = Rectangle.Empty;
            foreach (var c in members)
            {
                var spec = fields.FirstOrDefault(f => f.Control == c);
                var r = spec != null ? spec.Outer : c.Bounds;
                bounds = bounds.IsEmpty ? r : Rectangle.Union(bounds, r);
            }

            var card = Rectangle.FromLTRB(bounds.Left - 12, bounds.Top - 36, bounds.Right + 12, bounds.Bottom + 12);
            if (!card.IntersectsWith(clip)) return;

            Theme.FillRound(g, Theme.Surface, card, Theme.CardRadius);
            Theme.DrawRound(g, Theme.SurfaceBorder, card, Theme.CardRadius);

            // 標題：強調色短條 + 小型大寫字
            Theme.FillRound(g, Theme.Accent, new RectangleF(card.Left + 14, card.Top + 15, 3, 11), 1.5f);
            TextRenderer.DrawText(g, caption, Theme.CaptionFont,
                new Rectangle(card.Left + 23, card.Top + 10, card.Width - 36, 20), Theme.TextSecondary,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
        }
    }
}
