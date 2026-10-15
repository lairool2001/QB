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
        // 視窗是否為作用中視窗（決定閒置時的重畫頻率）
        volatile bool windowIsActive;
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
            // richTextBox2 是「輸入編號跳轉」的按鍵接收框，平常瀏覽/看圖時都是它有焦點，只會收數字與 + - *；
            // 關掉它的輸入法，否則中文輸入法開著時 L 等字母快捷鍵會被輸入法攔走（變成 Keys.ProcessKey）
            richTextBox2.ImeMode = ImeMode.Disable;
            Activated += (s, e) => { windowIsActive = true; setToDraw(); };
            Deactivate += (s, e) => windowIsActive = false;
            ResizeRedraw = true;
            // 建立 handle 時標題列併入工作區會改變尺寸，但 Load 還沒算出 pictureBoxWidthLess；
            // Load 之前先拔掉 SizeChanged / Resize，否則內容區會被拉到整個視窗寬
            SizeChanged -= Form1_SizeChanged;
            Resize -= Form1_Resize;
            HandleCreated += (s, e) => chrome.CorrectInitialHeight();
            Load += (s, e) =>
            {
                captureGridMargins();
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

            setupCardSizeBar();

            navigationCard = new Control[]
            {
                richTextBox2, button1, button12, button2, button11, richTextBox3, richTextBox4, label2,
                button4, button17, button16, numericUpDown1, button5, comboBox2, button9, button14, button15,
                cardSizeCaption, cardSizeBar, cardSizeValue,
            };
            actionsCard = new Control[]
            {
                button7, button8, button13, richTextBox6, comboBox1, richTextBox7, button6, button10, richTextBox5,
            };

            // 操作模式色會改變欄位底色，需重畫外框
            richTextBox2.BackColorChanged += (s, e) => InvalidateField(richTextBox2);
            // 影片元件第一次顯示時才建立視窗，會被插到影片控制列上方；控制列每次出現都提到最前
            // 控制列浮在影片上（不停靠，顯示/隱藏時影片大小不變）。
            // 影片元件第一次顯示時才建立視窗，會被插到控制列上方；控制列每次出現都提到最前
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

        // ── 看圖縮放（右鍵 + 滾輪）────────────────────────────────────────
        // 以滑鼠位置為中心：滑鼠底下的那一點縮放後仍在滑鼠底下；每格約 18%（倍率縮放，大小圖手感一致）。
        // 第一次縮放時把 pictureBox2 固定成整個內容區，之後只改它的 ViewRect（圖片的顯示區域，可為小數）
        // 由 ZoomPictureBox 自己畫看得到的那一塊：不移動/縮放控制項，放再大也不抖、成本固定。
        // 動畫只對「寬度」緩動，高度由圖片比例、位置由錨點算出，錨點永遠精確停在滑鼠底下。
        const double imageZoomStep = 1.18;
        const double imageZoomEaseSeconds = 0.07;
        float imageZoomWidth, imageZoomTargetWidth;   // 目前 / 目標寬度
        PointF imageZoomAnchor;                        // 錨點（pictureBox2 座標，即滑鼠位置）
        float imageZoomU, imageZoomV;                  // 錨點在圖片上的相對位置（0~1）
        System.Windows.Forms.Timer imageZoomTimer;     // 第一次用到才建立
        bool imageZoomToRect;                          // true：正在動畫到指定矩形（空白鍵回到 100% 置中）
        RectangleF imageZoomRectTarget;
        Stopwatch imageZoomClock;
        double imageZoomLastTick;

        float imageAspect()
        {
            var image = pictureBox2.Image;
            return image == null || image.Height == 0 ? 1f : (float)image.Width / image.Height;
        }

        void zoomImageAt(int delta, Point cursor)
        {
            var image = pictureBox2.Image;
            if (image == null || delta == 0) return;

            if (imageZoomTimer == null)
            {
                imageZoomTimer = new System.Windows.Forms.Timer { Interval = 8 };
                imageZoomTimer.Tick += (s, e) => imageZoomTick();
                imageZoomClock = Stopwatch.StartNew();
            }

            if (!pictureBox2.ViewRect.HasValue)
            {
                enterImageViewMode();
                imageZoomWidth = imageZoomTargetWidth = pictureBox2.ViewRect.Value.Width;
            }

            // 以畫面上目前的圖為基準重新定錨，動畫中途換位置滾動也不會跳
            RectangleF view = pictureBox2.ViewRect.Value;
            var c = new PointF(cursor.X - pictureBox2.Left, cursor.Y - pictureBox2.Top);
            imageZoomAnchor = c;
            imageZoomU = view.Width > 0 ? (c.X - view.X) / view.Width : 0.5f;
            imageZoomV = view.Height > 0 ? (c.Y - view.Y) / view.Height : 0.5f;
            if (!imageZoomTimer.Enabled || imageZoomToRect)
            {
                // 空白鍵的回復動畫進行中又滾輪縮放：從畫面上目前的大小接手，不跳動
                imageZoomToRect = false;
                imageZoomWidth = imageZoomTargetWidth = view.Width;
            }

            // 倍率，並限制大小：最小 24px，最大為原圖 32 倍或視窗 20 倍（取大者）
            float minW = 24f;
            float maxW = Math.Max(image.Width * 32f, ClientSize.Width * 20f);
            float newW = (float)(imageZoomTargetWidth * Math.Pow(imageZoomStep, delta / 120.0));
            imageZoomTargetWidth = Math.Max(minW, Math.Min(maxW, newW));

            pictureBox2.HighQuality = false;   // 動畫中用快速內插
            imageZoomLastTick = imageZoomClock.Elapsed.TotalSeconds;
            imageZoomTimer.Start();
        }

        /// <summary>
        /// 把看圖切到 ViewRect 顯示：算出目前實際顯示的圖片區域（Zoom 模式會等比縮放後置中），
        /// 再把控制項鋪滿內容區，圖片看起來位置不變。之後拖曳（整個畫面都可以按住左鍵拖）與縮放都只改 ViewRect。
        /// </summary>
        void enterImageViewMode()
        {
            if (pictureBox2.Image == null || pictureBox2.ViewRect.HasValue) return;
            float aspect = imageAspect();
            Rectangle box = pictureBox2.Bounds;
            float fitW = Math.Min(box.Width, box.Height * aspect);
            float fitH = fitW / aspect;
            var shown = new RectangleF(box.Left + (box.Width - fitW) / 2f, box.Top + (box.Height - fitH) / 2f, fitW, fitH);

            var content = new Rectangle(0, chromeTop, ClientSize.Width, Math.Max(1, ClientSize.Height - chromeTop));
            pictureBox2.Dock = DockStyle.None;
            pictureBox2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            pictureBox2.SetBounds(content.X, content.Y, content.Width, content.Height);
            shown.Offset(-content.X, -content.Y);
            pictureBox2.HighQuality = true;
            pictureBox2.ViewRect = shown;
        }

        void imageZoomTick()
        {
            if (!pictureBox2.ViewRect.HasValue) { imageZoomTimer.Stop(); return; }

            double now = imageZoomClock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.05, now - imageZoomLastTick);
            imageZoomLastTick = now;
            float k = (float)(1 - Math.Exp(-dt / imageZoomEaseSeconds));

            if (imageZoomToRect)
            {
                // 位置與大小一起緩動；起點與終點比例相同，所以途中比例也不變
                RectangleF c = pictureBox2.ViewRect.Value, tg = imageZoomRectTarget;
                var next = new RectangleF(c.X + (tg.X - c.X) * k, c.Y + (tg.Y - c.Y) * k,
                                          c.Width + (tg.Width - c.Width) * k, c.Height + (tg.Height - c.Height) * k);
                bool arrived = Math.Abs(tg.X - next.X) < 0.05f && Math.Abs(tg.Y - next.Y) < 0.05f
                            && Math.Abs(tg.Width - next.Width) < 0.05f;
                if (arrived) next = tg;
                pictureBox2.ViewRect = next;
                imageZoomWidth = imageZoomTargetWidth = next.Width;
                if (arrived)
                {
                    imageZoomToRect = false;
                    imageZoomTimer.Stop();
                    pictureBox2.HighQuality = true;
                }
                return;
            }

            imageZoomWidth += (imageZoomTargetWidth - imageZoomWidth) * k;
            bool done = Math.Abs(imageZoomTargetWidth - imageZoomWidth) < 0.05f;
            if (done) imageZoomWidth = imageZoomTargetWidth;

            float w = imageZoomWidth;
            float h = w / imageAspect();
            pictureBox2.ViewRect = new RectangleF(imageZoomAnchor.X - imageZoomU * w, imageZoomAnchor.Y - imageZoomV * h, w, h);

            if (done)
            {
                imageZoomTimer.Stop();
                pictureBox2.HighQuality = true;   // 停下後用高品質重畫一次
            }
        }

        /// <summary>空白鍵：以動畫回到 100% 並置中；原圖比視窗大時改為剛好塞進視窗。</summary>
        void resetImageZoom()
        {
            var image = pictureBox2.Image;
            if (image == null) return;
            if (!pictureBox2.ViewRect.HasValue) enterImageViewMode();
            if (!pictureBox2.ViewRect.HasValue) return;

            if (imageZoomTimer == null)
            {
                imageZoomTimer = new System.Windows.Forms.Timer { Interval = 8 };
                imageZoomTimer.Tick += (s, e) => imageZoomTick();
                imageZoomClock = Stopwatch.StartNew();
            }
            // 原圖放得進視窗就 100%，放不進就縮到剛好塞進視窗（不放大）
            float fit = Math.Min(1f, Math.Min(pictureBox2.Width / (float)image.Width, pictureBox2.Height / (float)image.Height));
            float w = image.Width * fit, h = image.Height * fit;
            imageZoomRectTarget = new RectangleF((pictureBox2.Width - w) / 2f, (pictureBox2.Height - h) / 2f, w, h);
            imageZoomToRect = true;
            pictureBox2.HighQuality = false;
            imageZoomLastTick = imageZoomClock.Elapsed.TotalSeconds;
            imageZoomTimer.Start();
        }

        void stopImageZoomAnimation()
        {
            imageZoomTimer?.Stop();
        }

        // ── F12 視窗模式循環 ────────────────────────────────────────────
        // 進入最大化前記下一般視窗的位置大小，回到一般時直接套回；
        // 原本只設 WindowState = Normal，配合自訂標題列時大小會錯，檔案區也會維持填滿整個視窗
        Rectangle normalBoundsBeforeF12 = Rectangle.Empty;
        // 檔案區在一般模式下的邊距（以 Load 時的版面為準）
        int gridLeft, gridTopFromContent, gridRightMargin, gridBottomMargin;
        bool gridMarginsCaptured;

        void captureGridMargins()
        {
            if (gridMarginsCaptured) return;
            if (pictureBox1.Dock != DockStyle.None || pictureBox1.Right > ClientSize.Width - 100)
            {
                // 版面已被全螢幕模式改過（或尚未排好）：用設計時的邊距（Form1.Designer 的檔案區位置）
                gridLeft = 16;
                gridTopFromContent = 72;
                gridRightMargin = 336;
                gridBottomMargin = 16;
                return;
            }
            gridLeft = pictureBox1.Left;
            gridTopFromContent = pictureBox1.Top - chromeTop;
            gridRightMargin = ClientSize.Width - pictureBox1.Right;
            gridBottomMargin = ClientSize.Height - pictureBox1.Bottom;
            gridMarginsCaptured = true;
        }

        void cycleFullscreen()
        {
            loadingDraw = true;
            lockPictureBoxMouswDown = true;
            captureGridMargins();

            // 看影片/圖片/文字/輪播時切換 F12 不能離開檢視器：先記下各檢視器的顯示狀態，切換後套回
            bool video = vlcControl1.Visible, image = pictureBox2.Visible, text = richTextBox8.Visible, loop = loopPicture.Visible;
            bool viewerOpen = video || image || text || loop;

            if (pictureBox1.Dock == DockStyle.Fill)
            {
                // 無邊框全螢幕 → 一般
                FormBorderStyle = FormBorderStyle.Sizable;   // 自訂標題列與上邊距會跟著回來
                WindowState = FormWindowState.Normal;
                if (!normalBoundsBeforeF12.IsEmpty) Bounds = normalBoundsBeforeF12;
                pictureBox1.Dock = DockStyle.None;
                pictureBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                pictureBox1.SetBounds(gridLeft, chromeTop + gridTopFromContent,
                    Math.Max(1, ClientSize.Width - gridLeft - gridRightMargin),
                    Math.Max(1, ClientSize.Height - chromeTop - gridTopFromContent - gridBottomMargin));
                padding = 20;
                // 沒有開檢視器時才把瀏覽用的控制項還原（restoreControlVisible 會把影片等檢視器設回啟動時的隱藏狀態）
                if (!viewerOpen) restoreControlVisible();
            }
            else if (WindowState == FormWindowState.Maximized)
            {
                // 最大化 → 無邊框全螢幕（檔案區填滿、隱藏其他控制項）
                WindowState = FormWindowState.Normal;
                FormBorderStyle = FormBorderStyle.None;
                pictureBox1.Dock = DockStyle.Fill;
                padding = 5;
                WindowState = FormWindowState.Maximized;
            }
            else
            {
                // 一般 → 最大化
                normalBoundsBeforeF12 = Bounds;
                FormBorderStyle = FormBorderStyle.Sizable;
                WindowState = FormWindowState.Maximized;
                pictureBox1.Dock = DockStyle.None;
            }

            vlcControl1.Visible = video;
            pictureBox2.Visible = image;
            richTextBox8.Visible = text;
            loopPicture.Visible = loop;
            if (image) resetPictureBox2();   // 視窗大小變了，圖片重新置中

            // resetPictureBoxMain 在點陣圖大小沒變時會直接略過，導致 drawPanel 停在舊的停靠/位置；強制同步一次
            bitmapWidth = 0;
            resetPictureBoxMain();
            drawPanel.Dock = pictureBox1.Dock;
            if (pictureBox1.Dock == DockStyle.None)
                drawPanel.SetBounds(pictureBox1.Left, pictureBox1.Top, pictureBox1.Width, pictureBox1.Height);
            Invalidate();
            setToDraw();

            lockPictureBoxMouswDown = false;
            loadingDraw = false;
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

            // 捲軸的變粗/淡出動畫還沒結束時，要求下一幀以全速重畫，動畫才會順
            if (Math.Abs(hotTarget - scrollBarHot) > 0.01f || Math.Abs(awakeTarget - scrollBarAwake) > 0.01f)
                toDraw = true;
        }

        // ── 卡片大小拉霸（10% ~ 100%）──────────────────────────────────
        Label cardSizeCaption, cardSizeValue;
        FlatTrackBar cardSizeBar;

        void setupCardSizeBar()
        {
            // 放在 Home / List / Update 那一列下面，跟它們一樣靠右上錨定
            int top = button9.Bottom + 8;
            int left = button9.Left, right = button15.Right;
            var anchor = AnchorStyles.Top | AnchorStyles.Right;

            cardSizeCaption = new Label
            {
                Text = "卡片大小", AutoSize = false, Anchor = anchor,
                Bounds = new Rectangle(left, top, 64, 28),
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Theme.TextSecondary, BackColor = Theme.Surface,
            };
            cardSizeValue = new Label
            {
                AutoSize = false, Anchor = anchor,
                Bounds = new Rectangle(right - 46, top, 46, 28),
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Theme.Text, BackColor = Theme.Surface,
            };
            cardSizeBar = new FlatTrackBar
            {
                AutoSize = false, Anchor = anchor, TabStop = false,
                Minimum = 10, Maximum = 100, SmallChange = 5, LargeChange = 10,
                Bounds = new Rectangle(left + 66, top, right - 46 - 4 - (left + 66), 28),
                BackColor = Theme.Surface,
            };
            int saved = qbSetting?.cardScale ?? 0;
            cardSizeBar.Value = saved >= 10 && saved <= 100 ? saved : 100;
            cardSizeValue.Text = cardSizeBar.Value + "%";
            if (cardSizeBar.Value != 100) scale = cardSizeBar.Value / 100f;

            cardSizeBar.ValueChanged += (s, e) =>
            {
                cardSizeValue.Text = cardSizeBar.Value + "%";
                applyCardScale(cardSizeBar.Value / 100f);
                if (qbSetting != null) qbSetting.cardScale = cardSizeBar.Value;
            };
            cardSizeBar.MouseUp += (s, e) => FormMain.saveQBSetting();

            Controls.Add(cardSizeCaption);
            Controls.Add(cardSizeBar);
            Controls.Add(cardSizeValue);
            var tip = Theme.CreateToolTip(components);
            tip.SetToolTip(cardSizeBar, "檔案卡片大小");
        }

        /// <summary>改變卡片大小並保留閱讀位置（畫面頂端那張卡片維持在頂端）。</summary>
        void applyCardScale(float newScale)
        {
            if (Math.Abs(newScale - scale) < 0.0001f) return;
            float topOrdinal = 0, rowFraction = 0;
            bool keep = getLayoutMetrics(out int oldColumns, out int oldStep, out _, out _);
            if (keep)
            {
                float rowF = Math.Max(0f, (startTargetScrollY - targetScrollY) / oldStep);
                int row = (int)rowF;
                rowFraction = rowF - row;
                topOrdinal = row * oldColumns;
            }

            scale = newScale;   // 檔名字型會在下次繪製時依新縮放重建
            textToImage.Clear();   // 檔名快取圖是舊大小，清掉重做

            if (keep && getLayoutMetrics(out int newColumns, out int newStep, out _, out _))
            {
                int newRow = (int)(topOrdinal / newColumns);
                float y = startTargetScrollY - (newRow + rowFraction) * newStep;
                targetScrollY = scrollY = Math.Max(scrollBottomLimit, Math.Min(scrollTopLimit, y));
            }
            setToDraw();
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
