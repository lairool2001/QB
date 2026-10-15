using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace QuickBrowser
{
    /// <summary>
    /// 看圖用的 PictureBox。一般情況與 PictureBox 完全相同；
    /// 設定 ViewRect（圖片在本控制項內的位置與大小，可為小數、可超出邊界）後改由自己繪製：
    /// 只畫畫面上看得到的那一塊，縮放倍率再大成本也固定，位置可落在小數像素上不會抖動，
    /// 控制項本身不需移動或縮放（不受視窗座標 ±32767 的限制）。
    /// </summary>
    public class ZoomPictureBox : PictureBox
    {
        RectangleF? viewRect;
        bool highQuality = true;
        readonly ImageAttributes edgeClamp = new ImageAttributes();

        public ZoomPictureBox()
        {
            // 裁切來源區域時避免邊緣取樣到透明而出現細線
            edgeClamp.SetWrapMode(WrapMode.TileFlipXY);
        }

        /// <summary>圖片在本控制項座標中的顯示區域；null 表示一般 PictureBox 行為。</summary>
        public RectangleF? ViewRect
        {
            get => viewRect;
            set
            {
                viewRect = value;
                Invalidate();
            }
        }

        /// <summary>動畫進行中用較快的雙線性內插，停下後改用高品質。</summary>
        public bool HighQuality
        {
            get => highQuality;
            set
            {
                if (highQuality == value) return;
                highQuality = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            var image = Image;
            if (!viewRect.HasValue || image == null)
            {
                base.OnPaint(pe);
                return;
            }

            RectangleF view = viewRect.Value;
            if (view.Width <= 0 || view.Height <= 0) return;

            // 只畫與畫面重疊的部分：目的區域 = 圖片區域 ∩ 控制項，再換算回原圖的來源區域
            RectangleF dest = RectangleF.Intersect(view, new RectangleF(0, 0, Width, Height));
            if (dest.Width <= 0 || dest.Height <= 0) return;

            float sx = image.Width / view.Width;
            float sy = image.Height / view.Height;
            RectangleF src = new RectangleF((dest.X - view.X) * sx, (dest.Y - view.Y) * sy, dest.Width * sx, dest.Height * sy);

            var g = pe.Graphics;
            g.InterpolationMode = highQuality ? InterpolationMode.HighQualityBicubic : InterpolationMode.Bilinear;
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.CompositingQuality = highQuality ? CompositingQuality.HighQuality : CompositingQuality.HighSpeed;

            PointF[] destPoints =
            {
                new PointF(dest.Left, dest.Top),
                new PointF(dest.Right, dest.Top),
                new PointF(dest.Left, dest.Bottom),
            };
            g.DrawImage(image, destPoints, src, GraphicsUnit.Pixel, edgeClamp);

            // 本專案沒有在 pictureBox2 的 Paint 事件上掛其他繪製，所以這裡不需另外觸發 Paint 事件
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) edgeClamp.Dispose();
            base.Dispose(disposing);
        }
    }
}
