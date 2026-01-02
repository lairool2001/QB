using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using unvell.D2DLib;
using File = System.IO.File;
using System.Drawing.Imaging;
using System.Threading;
using Amib.Threading;
using Action = Amib.Threading.Action;
using System.Windows.Controls;
using System.Diagnostics;
using System.Windows;

namespace QuickBrowser
{
    public partial class LoopPicture : System.Windows.Forms.UserControl
    {
        public SmartThreadPool smartThreadPool = new SmartThreadPool();
        public void addJob(System.Action action)
        {
            WorkItemCallback workItemCallback = (obj) => { action(); return true; };
            smartThreadPool.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
        }

        private Thread refreshThread;
        public Action onExit;
        Action refresh;
        public PictureBox drawPanel { protected set; get; }
        private int need = 8;
        public LoopPicture()
        {
            InitializeComponent();

            drawPanel = new PictureBox();
            drawPanel.Dock = DockStyle.Fill;
            Controls.Add(drawPanel);

            refresh = drawPanel.Refresh;

            //drawPanel.draw += draw;
            drawPanel.MouseWheel += DrawPanel_MouseWheel;
            drawPanel.MouseDown += DrawPanel_MouseDown;

            newGraphic();
        }
        RectangleF view; 
        void newGraphic()
        {
            if (drawPanel.Width == 0 || drawPanel.Height == 0) return;
            refreshThread?.Abort();
            Bitmap bitmap = new Bitmap(drawPanel.Width, drawPanel.Height);
            drawPanel.Image = bitmap;
            g = Graphics.FromImage(drawPanel.Image);
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            view = new RectangleF(0, 0, drawPanel.Width, drawPanel.Height);
        }

        private void DrawPanel_MouseDown(object sender, MouseEventArgs e)
        {
            onExit?.Invoke();
        }

        private volatile bool change;
        private float wheelDelta;
        private void DrawPanel_MouseWheel(object sender, MouseEventArgs e)
        {
            wheelDelta = e.Delta;
            totalScroll2 += wheelDelta;
            triggerDraw = true;
        }

        float delta => totalScroll - startTop;

        class Draw
        {
            static List<Draw> drawList = new List<Draw>();
            public static Draw getDraw()
            {
                if (drawList.Count == 0)
                {
                    return new Draw();
                }
                else
                {
                    var d = drawList[drawList.Count - 1];
                    drawList.RemoveAt(drawList.Count - 1);
                    d.fIndex = 0;
                    d.frameCount = 0;
                    d.width = 0;
                    d.height = 0;
                    return d;
                }
            }

            public static void backToCache(Draw draw)
            {
                if (drawList.Contains(draw)) return;
                drawList.Add(draw);
            }
            public FrameDimension dim;
            public Bitmap bitmapX;
            public Graphics g;
            public Bitmap[] d2bitmap;
            public void loadBitmap(Bitmap bitmap)
            {
                bitmapX = bitmap;
                FrameDimension f;
                lock (bitmap)
                {
                    f = new FrameDimension(bitmap.FrameDimensionsList[0]);
                    frameCount = bitmap.GetFrameCount(f);
                }
                d2bitmap = new Bitmap[frameCount];
                for (int i = 0; i < frameCount; i++)
                {
                    bitmapX = new Bitmap(bitmap.Width, bitmap.Height);
                    Graphics gx = Graphics.FromImage(bitmapX);
                    try
                    {
                        bitmap.SelectActiveFrame(f, i);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                    }
                    lock (bitmap)
                    {
                        gx.DrawImage(bitmap, new RectangleF(0, 0, bitmap.Width, bitmap.Height));
                    }
                    d2bitmap[i] = bitmapX;
                    gx.Dispose();
                }
                //g.Dispose();
            }

            public void dispose()
            {
                bitmapX.Dispose();
                for (int i = 0; i < d2bitmap.Length; i++)
                {
                    d2bitmap[i].Dispose();
                }

                d2bitmap = null;
            }

            public void draw(Graphics graphics, RectangleF rect2)
            {
                if (fIndex >= frameCount) fIndex = 0;
                if (d2bitmap == null) return;
                graphics.DrawImage(d2bitmap[fIndex], rect2);
                fIndex++;
            }
            public float width, height;
            public int frameCount;
            public RectangleF rect;
            public int fIndex;
        }
        private Dictionary<string, Draw> pathToDraw = new Dictionary<string, Draw>();
        private List<string> bitmapPaths = new List<string>();
        List<(int i, Draw d)> intList = new List<(int, Draw)>();
        Graphics g;
        bool triggerOK;
        void draw()
        {
            if (bitmapPaths.Count == 0)
            {
                return;
            }
        start:
            caculatBitmapRect();
            g.Clear(Color.White);
            change = false;
            float h = intList[2].d.rect.Height;
            float h2 = intList[3].d.rect.Height;
            if (delta <= -h2)
            {
                offsetIndex++;
                change = true;
            }
            if (delta >= h)
            {
                offsetIndex--;
                change = true;
            }

            if (change)
            {
                totalScroll = 0;
                caculatBitmapRect();
                for (int i = 0; i < 3; i++)
                {
                    totalScroll -= intList[i].d.rect.Height;
                }
                totalScroll2 = totalScroll;
                goto start;
            }

            for (int i = 0; i < intList.Count; i++)
            {
                int i2 = intList[i].i;
                var draw = intList[i].d;
                var RectangleF = draw.rect;
                if (i2 >= bitmapPaths.Count) continue;
                var path = bitmapPaths[i2];

                if (!pathToDraw.TryGetValue(path, out var drawX))
                {
                    pathToDraw[path] = drawX;
                }

                Action DrawImage = () =>
                {
                    if (draw.fIndex >= draw.frameCount)
                    {
                        draw.fIndex = 0;
                    }
                    if (view.IntersectsWith(RectangleF))
                    {
                        draw.draw(g, RectangleF);
                    }
                };
                float height = RectangleF.Height;
                if (i2 == 0 && i2 == bitmapPaths.Count - 1)
                {
                    g.DrawLine(Pens.Black, new PointF(0, RectangleF.Y), new PointF(Width, RectangleF.Y));
                    RectangleF.Y += 1;
                    DrawImage();
                    RectangleF.Y += height;
                    g.DrawLine(Pens.Black, new PointF(0, RectangleF.Y), new PointF(Width, RectangleF.Y));
                }
                else if (i2 == 0)
                {
                    g.DrawLine(Pens.Black, new PointF(0, RectangleF.Y), new PointF(Width, RectangleF.Y));
                    RectangleF.Y += 1;
                    DrawImage();
                }
                else if (i2 == bitmapPaths.Count - 1)
                {
                    DrawImage();
                    RectangleF.Y += height;
                    g.DrawLine(Pens.Black, new PointF(0, RectangleF.Y), new PointF(Width, RectangleF.Y));
                }
                else
                {
                    DrawImage();
                }
            }
        }

        List<string> drawingPath = new List<string>();
        Dictionary<int, Draw> intOToDraw = new Dictionary<int, Draw>();
        Dictionary<string, Bitmap> pathToImage = new Dictionary<string, Bitmap>();
        private float top;
        private void caculatBitmapRect()
        {
            for (int i = 0; i < intList.Count; i++)
            {
                var old = intList[i];
                Draw.backToCache(old.d);
            }
            intList.Clear();

            drawingPath.Clear();
            top = totalScroll;
            for (int i = 0; i < need * 2; i++)
            {
                int i2 = i - (need - 1) + offsetIndex;
                while (i2 < 0)
                {
                    i2 += bitmapPaths.Count;
                }

                i2 %= bitmapPaths.Count;
                var path = bitmapPaths[i2];
                if (!pathToImage.ContainsKey(path))
                {
                    pathToImage[path] = null;
                    addJob(() =>
                    {
                        pathToImage[path] = new Bitmap(path);
                    });
                }
            }
            for (int i = 0; i < need; i++)
            {
                int i2 = i - (need / 2 - 1) + offsetIndex;
                while (i2 < 0)
                {
                    i2 += bitmapPaths.Count;
                }
                i2 %= bitmapPaths.Count;

                var path = bitmapPaths[i2];
                drawingPath.Add(path);

                if (!pathToDraw.TryGetValue(path, out Draw draw))
                {
                    draw = new Draw();
                    Bitmap bitmap = null;
                    if (pathToImage.TryGetValue(path, out var image) && image != null)
                    {
                        bitmap = image;
                    }
                    else
                    {
                        pathToImage[path] = null;
                        addJob(() =>
                        {
                            try
                            {
                                bitmap = new Bitmap(path);
                                pathToImage[path] = bitmap;
                            }
                            catch (Exception ex)
                            {
                                bitmap = null;
                            }
                        });
                    }
                    if (bitmap != null)
                    {
                        draw.bitmapX = bitmap;
                        draw.loadBitmap(bitmap);
                        draw.width = bitmap.Width;
                        draw.height = bitmap.Height;
                        pathToDraw[path] = draw;
                    }
                }

                if (!intOToDraw.TryGetValue(i, out Draw draw2))
                {
                    draw2 = new Draw();
                    intOToDraw[i] = draw2;
                }

                draw2.bitmapX = draw.bitmapX;
                draw2.d2bitmap = draw.d2bitmap;
                draw2.frameCount = draw.frameCount;
                draw2.g = draw.g;
                draw2.width = draw.width;
                draw2.height = draw.height;

                intList.Add((i2, draw2));

                RectangleF RectangleF = new RectangleF(Width / 2 - draw2.width / 2, top, draw2.width, draw2.height);
                float widthWithPadding = Width - 40;
                if (RectangleF.Width > widthWithPadding)
                {
                    RectangleF.Height *= widthWithPadding / RectangleF.Width;
                    RectangleF.Width = widthWithPadding;
                    RectangleF = new RectangleF(Width / 2 - RectangleF.Width / 2, top, RectangleF.Width, RectangleF.Height);
                }
                draw2.rect = RectangleF;

                if (i2 == 0 && i2 == bitmapPaths.Count - 1)
                {
                    top += 2;
                }
                else if (i2 == 0 || i2 == bitmapPaths.Count - 1)
                {
                    top += 1;
                }
                float h = (float)Math.Floor(RectangleF.Height);
                top += h;
            }
        }

        public int offsetIndex = 0;
        private void LoopPicture_Load(object sender, EventArgs e)
        {
        }

        private float startTop
        {
            get
            {
                float top = 0;
                for (int i = 0; i < 3; i++)
                {
                    float h = intList[i].d.rect.Height;
                    top -= h;
                }
                return top;
            }
        }
        Thread drawThread;
        public void set(List<FileDirectoryCard> list, int mode = 0)
        {
            if (mode <= 0)
            {
                offsetIndex = 0;
                totalScroll = 0;
            }
            bitmapPaths.Clear();
            for (int i = 0; i < list.Count; i++)
            {
                var card = list[i];
                if (card.isImage && File.Exists(card.fullPath))
                {
                    bitmapPaths.Add(card.fullPath);
                }
            }

            if (bitmapPaths.Count == 0)
            {
                return;
            }

            if (mode <= 1)
            {
                totalScroll = 0;

                caculatBitmapRect();
                for (int i = 0; i <= Math.Floor(need * 0.5f)-1.1; i++)
                {
                    totalScroll -= intList[i].d.rect.Height;
                }
            }
            totalScroll2 = totalScroll;
            Visible = true;
        }

        RectangleF getGoodWitdhRect(float width, float height)
        {
            RectangleF RectangleF = new RectangleF(Width / 2 - width / 2, 0, width, height);
            if (RectangleF.Width > Width)
            {
                float widthWithPadding = Width - 40;
                RectangleF.Height *= widthWithPadding / RectangleF.Width;
                RectangleF.Width = widthWithPadding;
                RectangleF = new RectangleF(Width / 2 - RectangleF.Width / 2, 0, RectangleF.Width, RectangleF.Height);
            }

            return RectangleF;
        }

        private float totalScroll;
        private float totalScroll2;

        private bool refreshing;
        private void LoopPicture_VisibleChanged(object sender, EventArgs e3)
        {
            refreshing = Visible;
            if (Visible)
            {
                newGraphic();
                Action refresh = drawPanel.Refresh;
                refreshThread = new Thread(new ThreadStart(() =>
                {
                    while (refreshing)
                    {
                        int add = scroll;
                        if (totalScroll2 > totalScroll)
                        {
                            totalScroll += add;
                            if (totalScroll2 < totalScroll)
                            {
                                totalScroll = totalScroll2;
                            }

                        }
                        else if (totalScroll2 < totalScroll)
                        {
                            totalScroll -= add;
                            if (totalScroll2 > totalScroll)
                            {
                                totalScroll = totalScroll2;
                            }
                        }
                        if (refreshing)
                        {
                            draw();
                        }
                        Invoke(refresh);
                        /*if (triggerDraw)
                        {
                            draw();
                            Invoke(refresh);
                            triggerDraw = false;
                        }*/
                        Thread.Sleep(1);
                    }
                }));
                refreshThread.Priority = ThreadPriority.Highest;
                refreshThread.Start();
            }
            if (!Visible)
            {
                refreshThread?.Abort();
                g.Dispose();
                var e = pathToDraw.GetEnumerator();
                while (e.MoveNext())
                {
                    e.Current.Value.dispose();
                }
                intOToDraw.Clear();
                pathToDraw.Clear();
                bitmapPaths.Clear();
            }
        }
        int scroll = 50;
        private void LoopPicture_Resize(object sender, EventArgs e)
        {
            scroll = Height / 20;
            newGraphic();
        }
        bool triggerDraw;
        private void LoopPicture_Scroll(object sender, ScrollEventArgs e)
        {
        }
    }
}

