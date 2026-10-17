using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace QuickBrowser
{
    /// <summary>
    /// 檔案區用的 PictureBox。Layered = false 時與一般 PictureBox 相同；
    /// Layered = true 時改成分層子視窗（WS_EX_LAYERED，需 Windows 8 以上與 app.manifest 宣告），
    /// 每次 Refresh 以 UpdateLayeredWindow 把點陣圖連同透明度交給系統合成，
    /// 透明的地方會看到（也點得到）下面的 WebView2 網頁背景。
    /// </summary>
    public class LayeredPictureBox : PictureBox
    {
        const int GWL_EXSTYLE = -20;
        const int WS_EX_LAYERED = 0x00080000;
        const int ULW_ALPHA = 0x02;
        const byte AC_SRC_OVER = 0x00;
        const byte AC_SRC_ALPHA = 0x01;

        [StructLayout(LayoutKind.Sequential)] struct SIZE { public int cx, cy; }
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int x, y; }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct BLENDFUNCTION { public byte BlendOp, BlendFlags, SourceConstantAlpha, AlphaFormat; }

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr hdcDst, IntPtr pptDst, ref SIZE psize,
            IntPtr hdcSrc, ref POINT pptSrc, uint crKey, ref BLENDFUNCTION pblend, int dwFlags);
        [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr hdc, IntPtr h);
        [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr h);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h, int i);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern IntPtr SetWindowLongPtr(IntPtr h, int i, IntPtr v);

        bool layered;
        object drawLock;

        /// <summary>
        /// 繪圖執行緒在點陣圖上作畫時要持有這把鎖。GDI+ 的點陣圖不能兩個執行緒同時使用：
        /// 繪圖中 UI 執行緒又去 GetHbitmap 會兩邊互等、整個程式凍結。
        /// Present 拿不到鎖就跳過，等繪圖執行緒畫完這一幀後的 Refresh 再送。
        /// </summary>
        public object DrawLock => drawLock ??= new object();

        /// <summary>是否以逐像素透明的分層視窗呈現（網頁背景模式）。</summary>
        public bool Layered
        {
            get => layered;
            set
            {
                if (layered == value) return;
                layered = value;
                if (!IsHandleCreated) return;
                long ex = GetWindowLongPtr(Handle, GWL_EXSTYLE).ToInt64();
                ex = value ? ex | WS_EX_LAYERED : ex & ~WS_EX_LAYERED;
                SetWindowLongPtr(Handle, GWL_EXSTYLE, new IntPtr(ex));
                if (value) Present();
                else Invalidate();
            }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var cp = base.CreateParams;
                if (layered) cp.ExStyle |= WS_EX_LAYERED;
                return cp;
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (layered) Present();
        }

        public override void Refresh()
        {
            if (layered) Present();
            else base.Refresh();
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (layered) Present();
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            // 分層視窗的內容由 UpdateLayeredWindow 提供，不走 WM_PAINT
            if (layered) return;
            base.OnPaint(pe);
        }

        protected override void OnPaintBackground(PaintEventArgs pevent)
        {
            if (layered) return;
            base.OnPaintBackground(pevent);
        }

        /// <summary>把目前的 Image（32 位元預乘透明度）送給系統合成。</summary>
        void Present()
        {
            if (!IsHandleCreated || !(Image is Bitmap bitmap)) return;
            if (Width <= 0 || Height <= 0) return;
            if (!System.Threading.Monitor.TryEnter(DrawLock)) return;   // 繪圖執行緒正在畫，畫完會再送
            try
            {
                presentLocked(bitmap);
            }
            finally
            {
                System.Threading.Monitor.Exit(DrawLock);
            }
        }

        void presentLocked(Bitmap bitmap)
        {
            IntPtr hBitmap;
            int w = Width, h = Height;
            try
            {
                if (bitmap.Width == w && bitmap.Height == h)
                {
                    // 背景色 alpha = 0：保留點陣圖原本的透明度
                    hBitmap = bitmap.GetHbitmap(Color.FromArgb(0));
                }
                else
                {
                    // UpdateLayeredWindow 會把視窗改成送出的大小：點陣圖尺寸跟控制項不同（最大化、縮放中）時，
                    // 照一般 PictureBox 的方式貼在左上角，補成控制項大小再送，不能跳過（跳過會整片看不到）
                    using (var frame = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                    {
                        using (var g = Graphics.FromImage(frame))
                        {
                            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            g.DrawImageUnscaled(bitmap, 0, 0);
                        }
                        hBitmap = frame.GetHbitmap(Color.FromArgb(0));
                    }
                }
            }
            catch (InvalidOperationException) { return; }   // 繪圖執行緒正在用這張圖，下一幀再送
            catch (ArgumentException) { return; }           // 圖已被換掉／釋放

            IntPtr screenDc = GetDC(IntPtr.Zero);
            IntPtr memDc = CreateCompatibleDC(screenDc);
            IntPtr old = SelectObject(memDc, hBitmap);
            try
            {
                var size = new SIZE { cx = w, cy = h };
                var src = new POINT();
                var blend = new BLENDFUNCTION
                {
                    BlendOp = AC_SRC_OVER,
                    SourceConstantAlpha = 255,
                    AlphaFormat = AC_SRC_ALPHA,
                };
                UpdateLayeredWindow(Handle, IntPtr.Zero, IntPtr.Zero, ref size, memDc, ref src, 0, ref blend, ULW_ALPHA);
            }
            finally
            {
                SelectObject(memDc, old);
                DeleteObject(hBitmap);
                DeleteDC(memDc);
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }
    }
}
