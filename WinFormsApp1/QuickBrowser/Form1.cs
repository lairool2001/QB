using Amib.Threading;
using EverythingSharp.Enums;
using EverythingSharp.Fluent;
using LibVLCSharp.Shared;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Speech.Synthesis;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TsudaKageyu;
using Action = Amib.Threading.Action;
using BorderStyle = System.Windows.Forms.BorderStyle;
using Exception = System.Exception;
using Image = System.Drawing.Image;
using iwwsh = IWshRuntimeLibrary;
using Path = System.IO.Path;
using Rectangle = System.Drawing.Rectangle;

namespace QuickBrowser
{
    public partial class Form1 : Form
    {
        private LoopPicture _loopPicture;

        private LoopPicture loopPicture
        {
            get
            {
                if (_loopPicture == null)
                {
                    _loopPicture = new LoopPicture();
                    _loopPicture.Dock = DockStyle.Fill;
                    _loopPicture.TabStop = false;
                    Controls.Add(_loopPicture);
                }

                return _loopPicture;
            }
        }
        private PictureBox _drawPanel;

        public PictureBox drawPanel
        {
            get
            {
                if (_drawPanel == null)
                {
                    _drawPanel = new PictureBox();
                    _drawPanel.Dock = pictureBox1.Dock;
                    _drawPanel.SetBounds(pictureBox1.Bounds.X, pictureBox1.Bounds.Y, pictureBox1.Bounds.Width, pictureBox1.Bounds.Height);
                    _drawPanel.MouseMove += pictureBox1_MouseMove;
                    _drawPanel.MouseDown += pictureBox1_MouseDown;
                    _drawPanel.MouseUp += pictureBox1_MouseUp_1;
                    _drawPanel.TabStop = false;
                    Controls.Add(_drawPanel);
                }

                return _drawPanel;
            }
        }
        public LibVLC _libVLC;
        private MediaPlayer _mediaPlayer;
        public Form1()
        {
            InitializeComponent();
            Core.Initialize();
            _libVLC = new LibVLC();
            _mediaPlayer = new MediaPlayer(_libVLC);
            vlcControl1.MediaPlayer = _mediaPlayer;
        }

        bool isDisk;
        private float selectDelta => scrollY - selectStart;
        float selectStart;
        RectangleF selectRectangle
        {
            get
            {
                if (!selectRectangleMode)
                {
                    return RectangleF.Empty;
                }
                RectangleF _selectRectangle;
                PointF min, max;
                min = new PointF(Math.Min(p1.Value.X, p2.Value.X), Math.Min(p1.Value.Y, p2.Value.Y));
                max = new PointF(Math.Max(p1.Value.X, p2.Value.X), Math.Max(p1.Value.Y, p2.Value.Y));

                if (selectStart > scrollY && p2.Value.Y > p1.Value.Y)
                {
                    min.Y += selectDelta;
                }
                else if (selectStart < scrollY && p2.Value.Y < p1.Value.Y)
                {
                    //max.Y += selectDelta;
                }
                _selectRectangle = new RectangleF(min.X, min.Y, max.X - min.X, max.Y - min.Y);
                _selectRectangle.Width = Math.Max(1, _selectRectangle.Width);
                _selectRectangle.Height = Math.Max(1, _selectRectangle.Height);
                if (comboBox1IndexThreadSafe == 1)
                {

                }
                else
                {
                    _selectRectangle.X += bitmapWidth - pictureBox1.Width;
                }
                _selectRectangle.Y += (bitmapHeight - pictureBox1.Height) / 2;
                return _selectRectangle;
            }
        }
        //public static List<Form1> openList = new List<Form1>();
        List<string> history = new List<string>();
        Graphics g;
        Bitmap b;
        SmartThreadPool smartThreadPool = new SmartThreadPool();
        SmartThreadPool smartThreadPool2 = new SmartThreadPool();
        SmartThreadPool smartThreadPool3 = new SmartThreadPool();
        public static SmartThreadPool smartThreadPoolFree = new SmartThreadPool();
        int pictureBoxWidthLess;
        Thread scrollSmoothMove;
        private int HeightMinus;
        public static HashSet<Form1> aliveForms = new HashSet<Form1>();
        static HashSet<string> drawed = new HashSet<string>();
        bool waitingDrawing;
        void newGraphic()
        {
            resetingGraphic = true;
            Bitmap bitmap = new Bitmap(drawPanel.Width, drawPanel.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            drawPanel.Image = bitmap;
            g = Graphics.FromImage(bitmap);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            resetingGraphic = false;
            drawPanel.Refresh();
        }
        private void Form1_Load(object sender, EventArgs e)
        {
            DoubleBuffered = true;
            addJobFree(() =>
            {
                while (!exit)
                {
                    while (pausing)
                    {
                        Thread.Sleep(100);
                    }
                    while (job1Queue.Count > 0)
                    {
                        if (job1Queue.TryDequeue(out var action))
                        {
                            WorkItemCallback workItemCallback = new WorkItemCallback((obj) =>
                            {
                                try
                                {
                                    action();
                                }
                                catch (Exception ex)
                                {

                                }
                                return true;
                            });
                            smartThreadPool.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
                        }
                    }
                    while (job2Queue.Count > 0)
                    {
                        if (job2Queue.TryDequeue(out var action))
                        {
                            WorkItemCallback workItemCallback = new WorkItemCallback((obj) =>
                            {
                                try
                                {
                                    action();
                                }
                                catch (Exception ex)
                                {

                                }
                                return true;
                            });
                            smartThreadPool2.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
                        }
                    }
                    while (job3Queue.Count > 0)
                    {
                        if (job3Queue.TryDequeue(out var action))
                        {
                            WorkItemCallback workItemCallback = new WorkItemCallback((obj) =>
                            {
                                try
                                {
                                    action();
                                }
                                catch (Exception ex)
                                {

                                }
                                return true;
                            });
                            smartThreadPool3.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
                        }
                    }
                    Thread.Sleep(1);
                }
            });

            workWidth = pictureBox1.Bounds.Width;
            workHeight = pictureBox1.Bounds.Height;

            aliveForms.Add(this);
            /*int a1=1, a2=2;
            Set2Way set2Way1 = new Set2Way(Set2Way.Direction.normal);
            set2Way1.set(ref a1,ref a2);
            Set2Way set2Way2= new Set2Way(Set2Way.Direction.reverse);
            a1 = 1;
            a2 = 2;
            set2Way2.set(ref a1, ref a2);*/
            newGraphic();

            label9.Text = "";

            lockComboBox2 = true;
            comboBox2.SelectedIndex = 0;
            lockComboBox2 = false;

            oldWidth = Width;
            oldHeight = Height;
            HeightMinus = ClientRectangle.Height - pictureBox1.Height;
            pictureBoxWidthLess = Width - pictureBox1.Width;
            pictureBox2.KeyDown += PictureBox2_KeyDown;
            label3.Text = "";
            Thread.CurrentThread.Priority = ThreadPriority.Highest;
            MouseWheel += mainMouseWheel;
            setGoodBitmapAndResetGraphic();
            //g = pictureBox1.CreateGraphics();

            refresh = drawPanel.Refresh;
            drawThread = new Thread(drawingThread);
            drawThread.Priority = ThreadPriority.Highest;
            //drawThread.IsBackground = true;
            drawThread.Start();

            scrollSmoothMove = new Thread(insideMove);
            scrollSmoothMove.Priority = ThreadPriority.Highest;
            scrollSmoothMove.Start();

            //richTextBox2.Visible = false;
            richTextBox2.Focus();
            DoubleBuffered = false;

            //oldRichText = richTextBox1.Text = qbSetting.home;

            pictureBox1.KeyDown += Form1_KeyDown;
            pictureBox2.KeyDown += Form1_KeyDown;

            recordControlsVisible();

            loopPicture.onExit += LoopPicture_OnExit;
            loopPicture.Visible = false;
            loopPicture.TabStop = false;
        }

        private void LoopPicture_OnExit()
        {
            hideViewersAndShowNormalControls();
            stopScroll = false;
            targetScrollY = scrollY;
            setToDraw();
        }

        bool selectRectangleMode
        {
            get
            {
                if (!p1.HasValue || !p2.HasValue) return false;
                float area = Math.Abs(p2.Value.X - p1.Value.X) * Math.Abs(p2.Value.Y - p1.Value.Y);
                return area > 50;
            }
        }
        private void mainMouseWheel(object sender, MouseEventArgs e)
        {
            if (pictureBox2.Visible)
            {
                if (MouseButtons == MouseButtons.Left)
                {
                }
                else if (MouseButtons == MouseButtons.Right)
                {
                    pictureBox2.Dock = DockStyle.None;
                    pictureBox2.Anchor = AnchorStyles.None;
                    pictureBox2.Width += e.Delta;
                    pictureBox2.Height += e.Delta;
                    pictureBox2.Left = Width / 2 - pictureBox2.Width / 2;
                    pictureBox2.Top = Height / 2 - pictureBox2.Height / 2;
                    oldMousePosX = e.X;
                    oldMousePosY = e.Y;
                }
                else if (MouseButtons == MouseButtons.None)
                {
                    if (selectCard == null) return;

                    if (changeImageIndex(e.Delta)) return;
                }
                return;
            }
            if (MouseButtons == MouseButtons.Left && !selectRectangleMode)
            {
                if (e.Delta > 0)
                {
                    back();
                }
                else
                {
                    forward();
                }
            }
            else if (MouseButtons == MouseButtons.Right && !selectRectangleMode)
            {
                if (e.Delta > 0)
                {
                    older();
                }
                else
                {
                    reOlder();
                }
            }
            else if (MouseButtons == MouseButtons.None || selectRectangleMode)
            {
                targetScrollY = scrollY + (picH + 50) * Math.Sign(e.Delta);
            }

            setToDraw();
        }

        private bool changeImageIndex(int delta)
        {
            bool over = false;
            int i = selectCard.index;
            int oldI = i;
            while (true)
            {
                if (delta < 0)
                {
                    i++;
                }
                else if (delta > 0)
                {
                    i--;
                }

                if (i < 0)
                {
                    i = 0;
                    over = true;
                }

                if (i >= cardList.Count)
                {
                    i = cardList.Count - 1;
                    over = true;
                }

                selectCard = cardList[i];
                if (selectCard.isImage)
                {
                    if (pictureBox2.Image != null) pictureBox2.Image.Dispose();
                    pictureBox2.Image = LoadImagePure(selectCard.fullPath);
                    resetPictureBox2();
                    pictureBox2.Refresh();
                    clearSelectedAndHandleUI();
                    selecteGoto = true;
                    gotoIndex = selectCard.index;
                    setToDraw();

                    if (over)
                    {
                        return true;
                    }
                    break;
                }

                if (over)
                {
                    if (!selectCard.isImage)
                    {
                        i = oldI;
                        selectCard = cardList[i];
                    }
                    return true;
                }
            }

            return false;
        }

        private void PictureBox2_KeyDown(object sender, KeyEventArgs e)
        {
        }
        const float startTargetScrollY = -25;
        volatile float targetScrollY = startTargetScrollY;
        float speedScrollY = 20f;
        float scrollThreadWait = 50;

        private bool stopScroll = false;
        uint oldIndex;
        private void insideMove()
        {
            while (!exit)
            {
                while (pausing)
                {
                    Thread.Sleep(100);
                }
                if (stopScroll)
                {

                }
                else if (targetScrollY > scrollY)
                {
                    scrollY += speedScrollY;
                    if (scrollY > targetScrollY)
                    {
                        scrollY = targetScrollY;
                    }

                    setToDraw();
                }
                else if (targetScrollY < scrollY)
                {
                    scrollY -= speedScrollY;
                    if (scrollY < targetScrollY)
                    {
                        scrollY = targetScrollY;
                    }

                    setToDraw();
                }
                else
                {
                    //Application.DoEvents();
                }
                /*if (lockGlobal)
                {
                    uint newIndex = ((uint)(-scrollY / getScrollHeight()));
                    if (newIndex < 0)
                    {
                        newIndex = 0;
                    }
                    if (newIndex != oldIndex)
                    {
                        goGlobal(filterWord, oldIndex);
                    }
                }*/
                Thread.Sleep(1);
            }
        }

        private void resetPictureBox2()
        {
            pictureBox2.SizeMode = PictureBoxSizeMode.Zoom;
            foreach (Control control in Controls)
            {
                control.Visible = false;
            }

            pictureBox2.Visible = true;
            //pictureBox2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            if (pictureBox2.Image != null && pictureBox2.Image.Width <= drawPanel.Width && pictureBox2.Image.Height <= drawPanel.Height)
            {
                pictureBox2.Width = pictureBox2.Image.Width;
                pictureBox2.Height = pictureBox2.Image.Height;
                pictureBox2.Left = Width / 2 - pictureBox2.Width / 2;
                pictureBox2.Top = (ClientRectangle.Height - pictureBox2.Height) / 2;
            }
            else
            {
                pictureBox2.Width = Width;
                pictureBox2.Height = ClientRectangle.Height;
                pictureBox2.Left = 0;
                pictureBox2.Top = 0;
            }
            pictureBox2.Dock = DockStyle.None;
        }

        string oldRichText = "";
        string getNowPath()
        {
            return nowPath;
        }
        string nowPath;
        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            nowPath = richTextBox1.Text.Replace("\n", "").Replace("\r", "").Replace("/", "\\");
            if (nowPath.StartsWith("s:"))
            {
                return;
            }
            while (File.Exists(nowPath))
            {
                nowPath = Path.GetDirectoryName(nowPath);
                lockGo = true;
                richTextBox1.Text = nowPath;
                lockGo = false;
            }
            if (nowPath.Length == 2)
            {
                nowPath += "\\";
            }
            else if (nowPath.Length == 1)
            {
                nowPath = computerPath;
            }
            /*if (nowPath.IndexOf('\\') != nowPath.LastIndexOf('\\') && nowPath.Last() == '\\')
            {
                nowPath = nowPath.Remove(nowPath.Length - 1);
            }*/
            goButNotSame(nowPath);
        }
        public void goButNotSame(string newPath)
        {
            if (oldRichText != newPath)
            {
                go(newPath);
            }
        }
        public void go(bool clearFilter = true, bool recordHistory = true, bool goSelect = false)
        {
            try
            {
                string nowPath = getNowPath();
                go(nowPath, clearFilter, recordHistory, goSelect);
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Hand.Play();
                MessageBox.Show(ex.ToString());
            }
        }

        private volatile bool maining = false;
        void callMain(Action action)
        {
            maining = true;
            if (action != null)
                pictureBox1.Invoke(action);
            maining = false;
        }

        void waitMaining()
        {
            while (maining)
            {
                Thread.Sleep(1);
            }
        }
        public void goOrGlobalGo(string to = null)
        {
            if (string.IsNullOrEmpty(to))
            {
                to = computerPath;
            }
            nowPath = to;
            loadNowState();
            if (to.StartsWith("s:"))
            {
                goGlobal(filterWord);
            }
            else
            {
                go(to);
            }
        }
        [DllImport("User32.dll")]
        public static extern Int32 SetForegroundWindow(int hWnd);

        private const string computerPath = "";
        bool lockGo = false;
        public void go(string nowPath, bool clearFilter = true, bool recordHistory = true, bool goSelect = true)
        {
            if (lockGo)
            {
                return;
            }
            //global = false;
            //textToImage.Clear();

            smartThreadPool.Cancel(true);
            smartThreadPool2.Cancel(true);
            smartThreadPool3.Cancel(true);

            addJob(() =>
            {
                going = true;

                string noneProcessPath = nowPath;

                string ext = "";
                bool isFile = File.Exists(nowPath);
                bool samePath = this.nowPath == nowPath;
                oldRichText = this.nowPath = nowPath;
                bool isFolder = true;
                bool isDisk = nowPath == computerPath;
                if (isFile)
                {
                    this.nowPath = Path.GetDirectoryName(nowPath);
                    ext = Path.GetExtension(nowPath);
                    bool isImage = imageFormat.Contains(ext);
                    if (isImage)
                    {
                        callMain(async () =>
                         {
                             drawing = true;
                             vlcControl1.Visible = false;
                             _mediaPlayer.Stop();
                             pictureBox2.Image = LoadImagePure(noneProcessPath);
                             pictureBox2.Show();
                             TopMost = true;
                             SetForegroundWindow(Handle.ToInt32());
                             resetPictureBox2();
                             TopMost = false;
                             richTextBox1.Text = this.nowPath;
                             FormMain.lastForm = this;
                             setToDraw();
                             await Task.Delay(100);
                             FormMain.lastForm.selectName = Path.GetFileName(nowPath);
                             triggerNoSelectGoTo = false;
                         });
                    }
                    bool isVideo = videoFormat.Contains(ext);
                    if (isVideo)
                    {
                        callMain(async () =>
                        {
                            drawing = false;
                            playAndOpenVideo(noneProcessPath);
                            pictureBox2.Hide();
                            TopMost = true;
                            SetForegroundWindow(Handle.ToInt32());
                            TopMost = false;
                            FormMain.lastForm = this;
                            setToDraw();
                            await Task.Delay(100);
                            FormMain.lastForm.selectName = Path.GetFileName(nowPath);
                            triggerNoSelectGoTo = false;
                        });
                    }
                }
                else
                {
                    if (recordHistory)
                    {
                        if (isDisk)
                        {
                            setNewHistroy(computerPath);
                        }
                        else if (isFolder)
                        {
                            setNewHistroy(noneProcessPath);
                        }
                    }

                    if (isFolder || isDisk)
                    {
                        updateFolderBackground(nowPath);
                        waitAllDrawOK = true;
                        loadingDraw = true;
                        setToDraw();

                        if (drawThread != null && !drawThread.IsAlive)
                        {
                            //drawThread?.Resume();
                        }
                        //textToImage.Clear();
                        clearSelectedAndHandleUI();
                        if (clearFilter)
                        {
                            callMain(() =>
                            {
                                lockSaveState = true;
                                richTextBox5.Text = "";
                                lockSaveState = false;
                            });
                        }
                        if (nowPath != computerPath)
                        {
                            if (nowPath.Length >= maxLengthPath.Length)
                            {
                                maxLengthPath = nowPath;
                            }
                            else
                            {
                                if (!maxLengthPath.StartsWith(nowPath))
                                    maxLengthPath = nowPath;
                            }
                        }

                        loadNowState();

                        callMain(() =>
                        {
                            //richTextBox3.Visible = true;
                            lockRichText3 = true;
                            richTextBox3.Text = "";
                            lockRichText3 = false;

                            richTextBox4.Text = "";

                            int s = richTextBox1.SelectionStart;
                            richTextBox1.Text = nowPath;
                            richTextBox1.SelectionStart = s;
                        });

                        selectCard = null;
                        selected.Clear();
                        cardList.Clear();

                        loadAllCard = false;

                        firstDrawIndex = -1;

                        if (isDisk) showDisk();
                        else if (isFolder) showFolder(nowPath);
                        if (!samePath)
                        {
                            targetScrollY = scrollY = startTargetScrollY;
                        }

                        //更新當前資料夾背景縮放模式
                        backgroundImagePath = "";

                        lockComboBox2 = false;

                        oldRichText = nowPath;

                        selectOld();
                    }
                    else
                    {
                        System.Media.SystemSounds.Hand.Play();
                    }
                }
            });
        }
        void selectOld()
        {
            addJob3(() =>
            {
                smartThreadPool.WaitForIdle();
                smartThreadPool2.WaitForIdle();
                while (sorting) Thread.Sleep(1);
                waitAllDrawOK = false;
                loadingDraw = false;
                histroyMode = false;
                /*if (!string.IsNullOrEmpty(selectName))
                {
                    going = false;
                    return;
                }*/

                for (int i = history.Count - 1; i >= 0; i--)
                {
                    var h = history[i];
                    if (Path.GetDirectoryName(h) != nowPath) continue;

                    selectName = Path.GetFileName(h);
                    Debug.WriteLine($"find dir same: {selectName}");
                    break;
                }

                callMain(() =>
                {
                    if (qbSetting.folderToImageShowType.TryGetValue(nowPath, out var index))
                    {
                        comboBox1.SelectedIndex = index;
                    }
                    else
                    {
                        comboBox1.SelectedIndex = 0;
                    }
                    showBackgroundPathText(nowPath);
                    updateFolderBackground(nowPath);
                });

                saveNowState();
                going = false;
                setToDraw();

            });
        }

        private void showBackgroundPathText(string nowPath)
        {
            //更新背景圖
            if (qbSetting.folderToBackgroundPath.TryGetValue(nowPath, out var path))
            {
                lockRichTextBox7 = true;
                richTextBox7.Text = path;
                lockRichTextBox7 = false;
            }
        }
        volatile float lockTime = 0;
        private void updateFolderBackground(string nowPath)
        {
            if (qbSetting.folderToBackgroundPath.ContainsKey(nowPath))
            {
                string newBackgroundImagePath = qbSetting.folderToBackgroundPath[nowPath];
                if (File.Exists(newBackgroundImagePath))
                {
                    loadingDraw = true;
                    if (backgroundImagePath == newBackgroundImagePath) return;
                    qbSetting.folderToBackgroundPath[nowPath] = newBackgroundImagePath;
                    backgroundImagePath = newBackgroundImagePath;
                    addJob(() =>
                    {
                        background = Image.FromFile(newBackgroundImagePath) as Bitmap;
                        setGoodBitmapAndResetGraphic();
                        setToDraw();
                    });
                }
                else
                {
                    backgroundImagePath = "";
                    if (background != null)
                    {
                        lock (background)
                        {
                            background = null;
                        }
                    }
                    string a;
                    qbSetting.folderToBackgroundPath.TryRemove(nowPath, out a);
                    setToDraw();
                }
            }
            else
            {
                loadingDraw = true;
                backgroundImagePath = "";
                background = null;
                callMain(() => { richTextBox7.Text = ""; });
                addJob(() =>
                {
                    setGoodBitmapAndResetGraphic();
                    setToDraw();
                });
            }
        }
        (string[], DateTime[]) GetFiles(string path)
        {
            IEnumerable<EverythingEntry> results = FormMain.everything
           .SearchFor($"parent:\"{path}\" file:")
           .WithOffset(0)
           .GetFields(RequestFlags.FullPathAndFileName | RequestFlags.DateRecentlyChanged)
           .Execute();
            List<string> strings = new List<string>();
            List<DateTime> changedz = new List<DateTime>();
            var e = results.GetEnumerator();
            while (e.MoveNext())
            {
                strings.Add(e.Current.FullPath);
                changedz.Add(e.Current.DateRecentlyChanged ?? DateTime.MinValue);
            }
            return (strings.ToArray(), changedz.ToArray());
        }
        (string[], DateTime[]) GetDirectories(string path)
        {

            IEnumerable<EverythingEntry> results = FormMain.everything
           .SearchFor($"parent:\"{path}\" attributes:D")
           .WithOffset(0)
           .GetFields(RequestFlags.FullPathAndFileName | RequestFlags.DateRecentlyChanged)
           .Execute();
            List<string> strings = new List<string>();
            List<DateTime> changedz = new List<DateTime>();
            var e = results.GetEnumerator();
            while (e.MoveNext())
            {
                strings.Add(e.Current.FullPath);
                changedz.Add(e.Current.DateRecentlyChanged ?? DateTime.MinValue);
            }
            return (strings.ToArray(), changedz.ToArray());
        }
        volatile bool sorting;
        void sortCardByComboBox2()
        {
            sorting = true;
            string nowPath = getNowPath();
            if (sortMode == 0)
            {
                filePathCacheListManager.folderToChildFileAndDirectoryList.Remove(nowPath);
                if (loadCacheCardList)
                {
                    pushCardListToCache(nowPath);
                }
                sorting = false;
                setToDraw();
                return;
            }
            loadingDraw = true;

            smartThreadPool2.WaitForIdle();
            addJob2(() =>
            {
                while (job1Queue.Count > 0 || adding)
                {
                    Thread.Sleep(1);
                }
                smartThreadPool.WaitForIdle();

                lock (cardList)
                {
                    switch (sortMode)
                    {
                        //new to old
                        case 1:
                            cardList = cardList.AsParallel().OrderByDescending(x =>
                            {
                                if (x.type == FileDirectoryCard.Type.disk)
                                {
                                    return DateTime.MinValue;
                                }
                                else
                                {
                                    if (x.fileSystemInfo == null) return DateTime.MinValue;
                                    return x.fileSystemInfo.LastWriteTime;
                                }
                            }).ToList();
                            break;
                        //name
                        case 2:
                            cardList = cardList.AsParallel()
                                .OrderBy(a =>
                                {
                                    string f = Path.GetFileNameWithoutExtension(a.fileName);
                                    var m = Regex.Match(f, @"\d+");
                                    StringBuilder stringBuilder = new StringBuilder();
                                    for (int i = 0; i < m.Groups.Count; i++)
                                    {
                                        stringBuilder.Append(m.Groups[i].Value);
                                    }
                                    if (m.Success)
                                    {
                                        if (int.TryParse(stringBuilder.ToString(), out int i))
                                        {
                                            return i;
                                        }
                                    }
                                    else
                                    {
                                        return a.fileName.GetHashCode();
                                    }
                                    return int.MinValue;
                                }).ToList();
                            //cardList = cardList.OrderBy(x => x.fileName).ToList();
                            //Array.Sort(array, (a, b) => { return a.fileName.CompareTo(b.fileName); });
                            break;
                        //size
                        case 3:
                            List<FileDirectoryCard> directoryCardList = new List<FileDirectoryCard>();
                            List<FileDirectoryCard> fileCardList = new List<FileDirectoryCard>();
                            foreach (var card in cardList)
                            {
                                if (card.type == FileDirectoryCard.Type.directory)
                                {
                                    directoryCardList.Add(card);
                                }
                                else
                                {
                                    fileCardList.Add(card);
                                }
                            }
                            ;
                            fileCardList = fileCardList.AsParallel().OrderByDescending(x =>
                            {
                                if (x == null) return 0;
                                return x.size;
                            }).ToList();
                            cardList.Clear();
                            cardList.AddRange(directoryCardList);
                            cardList.AddRange(fileCardList);
                            /*Array.Sort(array, (a, b) =>
                            {
                                if (File.Exists(a.fullPath) && File.Exists(b.fullPath))
                                {
                                    return (b.fileSystemInfo as FileInfo).Length.CompareTo((a.fileSystemInfo as FileInfo).Length);
                                }
                                return 0;
                            });*/
                            break;
                    }
                }
                Parallel.For(0, cardList.Count, (i) =>
                {
                    var card = cardList[i];
                    if (card != null)
                    {
                        card.index = i;
                        numToFileDctionaryCard[i] = card;
                    }
                });

                List<string> all = new List<string>();
                List<string> filez = new List<string>();
                List<string> dirOrDiskz = new List<string>();
                for (int i = 0; i < cardList.Count; i++)
                {
                    var card = cardList[i];
                    all.Add(card.fullPath);
                    if (card.type == FileDirectoryCard.Type.file)
                    {
                        filez.Add(card.fullPath);
                    }
                    else
                    {
                        dirOrDiskz.Add(card.fullPath);
                    }
                }
                filePathCacheListManager.folderToChildFileAndDirectoryList[nowPath] = all.ToArray();

                filePathCacheListManager.folderToChildFileList[nowPath] = filez.ToArray();
                filePathCacheListManager.folderToChildDirectoryList[nowPath] = dirOrDiskz.ToArray();

                updateZ();
                //Debug.WriteLine(stopwatch1.Elapsed.ToString());
                /*for (int i = 0; i < waitHandles.Count; i++)
                {
                    waitHandles[i].WaitOne();
                }*/
                if (loadCacheCardList)
                {
                    pushCardListToCache(nowPath);
                }

                sorting = false;
                setToDraw();

            });
        }
        void resetCache()
        {
        }
        string consoleDigital = "", maxConsoleDigital = "";
        volatile float scrollY;
        iwwsh.WshShell shell = new iwwsh.WshShell(); //Create a new WshShell Interface
        private void showDisk()
        {
            isDisk = true;
            callMain(() =>
            {
                Text = $"show all disk";
            });
            initBrowswerView();
            addJob(() =>
            {
                //flowLayoutPanel1.SuspendLayout();
                //resetFlowPanel();
                string[] diskzDrives = Environment.GetLogicalDrives();
                //Parallel.For(0, disk.Length, i =>
                for (int i = 0; i < diskzDrives.Length; i++)
                {
                    var disk = diskzDrives[i];
                    //setEmptyDirectoryCache(disk);
                    addAndInitDisk(i, disk);
                }//);
                max = diskzDrives.Length;
                Invoke(new Action(() =>
                {
                    label1.Text = maxConsoleDigital = max.ToString();
                }));
                setToDraw();
            });
        }

        private int sortMode;

        private void pushCardListToCache(string nowPath)
        {
            addJob2(() =>
            {
                List<FileDirectoryCard> list;
                smartThreadPool.WaitForIdle();
                lock (filePathCacheListManager)
                {
                    if (filePathCacheListManager.folderToCardList.TryGetValue(nowPath, out list) && !loadCacheCardList)
                    {
                        list.Clear();
                    }
                    else
                    {
                        list = new List<FileDirectoryCard>();
                        filePathCacheListManager.folderToCardList[nowPath] = list;
                    }
                    list.AddRange(cardList);
                }
                setToDraw();
            });
        }

        /// <summary>
        /// 已滿數位決定0字串格式化
        /// </summary>
        void updateZ()
        {
            StringBuilder ZBuilder = new StringBuilder();
            for (int z = 0; z < maxConsoleDigital.Length; z++)
            {
                ZBuilder.Append("0");
            }
            Z = ZBuilder.ToString();
        }
        int max;
        ConcurrentDictionary<int, FileDirectoryCard> numToFileDctionaryCard = new ConcurrentDictionary<int, FileDirectoryCard>();
        ConcurrentQueue<Image> waitDispose = new ConcurrentQueue<Image>();
        private List<FileDirectoryCard> cardList = new List<FileDirectoryCard>();
        List<WaitHandle> waitHandles = new List<WaitHandle>();
        //KillableThreadPool killableThreadPool = new KillableThreadPool();
        void initBrowswerView()
        {
            waitHandles.Clear();
            cardList.Clear();
            //smartThreadPool.Cancel(true);
            smartThreadPool2.Cancel(true);
            freeBitmap();
            numToFileDctionaryCard.Clear();
        }
        void addJob(Action action)
        {
            job1Queue.Enqueue(action);
        }
        void addJob2(Action action)
        {
            job2Queue.Enqueue(action);
        }
        void addJob3(Action action)
        {
            job3Queue.Enqueue(action);
        }
        void addJobFree(Action action)
        {
            WorkItemCallback workItemCallback = new WorkItemCallback((obj) => { action(); return true; });
            smartThreadPoolFree.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
        }
        void runFile(string startPath)
        {
            if (File.Exists(startPath))
            {
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo(startPath);
                    info.WorkingDirectory = Path.GetDirectoryName(startPath);
                    Process process = new Process();
                    process.StartInfo = info;
                    process.Start();

                    SpeechSynthesizer speech = new SpeechSynthesizer();
                    speech.Rate = 7;
                    speech.SpeakAsync($"開啟檔案");

                }
                catch
                {
                    System.Media.SystemSounds.Hand.Play();
                }
            }
            else
            {
                System.Media.SystemSounds.Hand.Play();
            }
        }
        //BMP, GIF, EXIF, JPG, PNG and TIFF
        readonly HashSet<string> imageFormat = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".bmp", ".gif", ".exif", ".jpg", ".jpeg", ".png", ".tiff", ".jfif" };
        readonly HashSet<string> videoFormat = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".mp4", ".wmv", ".webm", ".mov", ".avi", ".mkv", ".ts" };
        readonly HashSet<string> textFormat = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".txt", ".ini", ".cs", ".xml", ".php", ".log", ".bat", ".ps1" };
        volatile bool loadAllCard = false;
        bool loadCacheFileSystem = true, loadCacheCardList = false;

        void setEmptyDirectoryCache(string dir, int dis = 3)
        {
            try
            {
                if (dis <= 0) return;
                if (!Directory.Exists(dir)) return;
                string[] dz, fz;
                if (!filePathCacheListManager.folderToChildDirectoryList.TryGetValue(dir, out dz))
                {
                    dz = Directory.GetDirectories(dir);
                    filePathCacheListManager.folderToChildDirectoryList[dir] = dz;
                }

                if (!filePathCacheListManager.folderToChildFileList.ContainsKey(dir))
                {
                    fz = Directory.GetFiles(dir);
                    filePathCacheListManager.folderToChildFileList[dir] = fz;
                }

                //Parallel.For(0, dz.Length, i => { setEmptyDirectoryCache(dz[i], --dis); });
            }
            catch (Exception ex)
            {

            }
        }
        volatile bool adding;
        void showFolder(string path, bool reset = false)
        {
            isDisk = false;
            callMain(() =>
            {
                if (path.Length <= 3)
                {
                    Text = $"go {path} folder";
                }
                else
                {
                    Text = $"go {Path.GetFileName(path)} folder";
                }
            });

            initBrowswerView();

            addJob(() =>
            {
                bool loadCard = false;
                bool sorted;
                string[] dz = null, fz = null, all = null;
                DateTime[] dzdt, fzdt;
                try
                {
                    loadingDraw = true;
                    (fz, fzdt) = GetFiles(path);
                    (dz, dzdt) = GetDirectories(path);
                    cardList.Clear();
                }
                catch (Exception ex)
                {
                    loadingDraw = false;
                    Text = ex.Message;
                    System.Media.SystemSounds.Hand.Play();
                    //waitAllDrawOK = false;
                    return;
                }
                if (!loadCard)
                {
                    cardList.Clear();
                    adding = true;
                    for (int i = 0; i < dz.Length; i++)
                    {
                        addAndInitDirectoryCard(i, dz[i], dzdt[i]);
                        if (i % 50 == 0)
                        {
                            setToDraw();
                        }
                    }
                    for (int i = 0; i < fz.Length; i++)
                    {
                        addAndInitFileCard(dz.Length + i, fz[i], fzdt[i]);
                        if (i % 50 == 0)
                        {
                            setToDraw();
                        }
                    }
                    label1.Invoke(new Action(() =>
                    {
                        max = dz.Length + fz.Length - 1;
                        label1.Text = maxConsoleDigital = max.ToString();
                    }));
                    adding = false;
                }
                else
                {
                    label1.Invoke(new Action(() =>
                    {
                        max = cardList.Count - 1;
                        label1.Text = maxConsoleDigital = max.ToString();
                    }));
                    //waitAllDrawOK = false;
                    loadingDraw = false;
                    setToDraw();
                }
                sortCardByComboBox2();
            });

        }

        private void setNewHistroy(string newHistroy)
        {
            if (histroyIndex >= 0 && histroyIndex < history.Count)
            {
                if (!histroyMode)
                {
                    histroyIndex++;
                    history.Insert(histroyIndex, newHistroy);
                    history.RemoveRange(histroyIndex + 1, history.Count - histroyIndex - 1);
                }
                else
                {
                    history[histroyIndex] = newHistroy;
                }
            }
            else if (histroyIndex >= history.Count)
            {
                history.Add(newHistroy);
            }

            for (int i = history.Count - 1; i >= 1; i--)
            {
                var historyItem = history[i];
                var historyItem2 = history[i - 1];
                if (historyItem == historyItem2)
                {
                    history.RemoveAt(i);
                }
            }
        }
        void loadNowState()
        {
            callMain(() =>
            {
                lockComboBox2 = true;
                string now = getNowPath();
                if (!filePathCacheListManager.pathStateHashSet.ContainsKey(now))
                {
                    comboBox2.SelectedIndex = 0;
                    richTextBox5.Text = "";
                    sortMode = 0;
                    lockComboBox2 = false;

                    lockRichText5 = true;
                    filterWord = null;
                    filterWordz = null;
                    lockRichText5 = false;
                    return;
                }

                var state = filePathCacheListManager.pathStateHashSet[now];
                lockSaveState = true;

                comboBox2.SelectedIndex = state.sortMode;
                richTextBox5.Text = state.filter;
                sortMode = state.sortMode;
                lockComboBox2 = false;

                global = state.global;

                lockRichText5 = true;
                filterWord = state.filter;
                filterWordz = state.filter.Split(' ');
                lockRichText5 = false;

                if (!string.IsNullOrEmpty(filterWord))
                {
                    //targetScrollY = scrollY = 0;
                }

                //targetScrollY = scrollY = state.scrollY;
                //setToDraw();
                lockSaveState = false;
            });
        }
        void saveNowState()
        {
            if (lockSaveState) return;
            string now = getNowPath();
            if (!filePathCacheListManager.pathStateHashSet.ContainsKey(now))
            {
                filePathCacheListManager.pathStateHashSet[now] = new PathState();
            }
            var state = filePathCacheListManager.pathStateHashSet[now];
            callMain(() =>
            {
                state.sortMode = comboBox2.SelectedIndex;
                state.filter = richTextBox5.Text;
                state.scrollY = scrollY;
                state.global = global;
            });
        }
        Bitmap getSmallCenter(bool isImage, string f, string savePath)
        {
            Bitmap b = null;
            if (isImage)
            {
                b = getSmallerImageAndSave(f, savePath);
            }
            else
            {
                b = GetThumbnail(f, savePath);
            }
            return b;
        }

        float blockRight
        {
            get
            {
                if (comboBox1IndexThreadSafe == 1)
                {
                    return backgroundRect.Right;
                }
                else
                {
                    return drawPanel.Width;
                }
            }
        }
        object load = new object();
        string requestSmallerPath()
        {
            lock (filePathCacheListManager)
            {
                Interlocked.Add(ref filePathCacheListManager.imageFlow, 1);
                int flow = filePathCacheListManager.imageFlow;
                string smallerPath = Path.Combine("Cache", filePathCacheListManager.imageFlow + ".png");
                string imagePath;
                filePathCacheListManager.JPGFileToimagePath.TryRemove(smallerPath, out imagePath);
                if (imagePath != null)
                {
                    string cacheJPEG;
                    filePathCacheListManager.imagePathToCacheJPGFile.TryRemove(imagePath, out cacheJPEG);
                }

                if (filePathCacheListManager.imageFlow > FilePathCacheListManager.imageFlowMax)
                {
                    filePathCacheListManager.JPGFileToimagePath.Clear();
                    filePathCacheListManager.imagePathToCacheJPGFile.Clear();
                    imagePathToThumbnailCachePool.Clear();
                    filePathCacheListManager.imageFlow = 0;
                }
                return smallerPath;
            }
        }
        /// <summary>
        /// 取得 .lnk/.url 檔案所對應的目標路徑
        /// </summary>
        /// <param name="file">.lnk 或 .url 檔案完整路徑</param>
        /// <returns>目標路徑，無法解析則回傳空字串</returns>
        private string CreateShortcut(string file)
        {
            if (string.IsNullOrEmpty(file) || !File.Exists(file)) return "";
            string ext = Path.GetExtension(file).ToLowerInvariant();
            bool lnk = ext == ".lnk";
            bool url = ext == ".url";
            if (!lnk && !url) throw new Exception("Supplied file must be a .LNK or .URL file");

            string path = null;

            // 對 .url 檔專屬處理
            if (url)
            {
                try
                {
                    foreach (var line in File.ReadAllLines(file))
                    {
                        if (line.StartsWith("URL=", StringComparison.OrdinalIgnoreCase))
                        {
                            // 取出 URL= 右側
                            return line.Substring(4).Trim();
                        }
                    }
                }
                catch { }
                return "";
            }

            // 對 .lnk 檔主要路徑解析
            if (lnk)
            {
                try
                {
                    // 建議用 COM 物件讀取法
                    IWshRuntimeLibrary.WshShell shell = new IWshRuntimeLibrary.WshShell();
                    IWshRuntimeLibrary.IWshShortcut shortcut = (IWshRuntimeLibrary.IWshShortcut)
                        shell.CreateShortcut(file);

                    if (!string.IsNullOrEmpty(shortcut.TargetPath))
                        return shortcut.TargetPath;

                    // 有可能只解析到工作路徑等，酌情補充
                    if (!string.IsNullOrEmpty(shortcut.WorkingDirectory))
                        return shortcut.WorkingDirectory;

                    // 若 TargetPath 取不到，可加 Binary 兜底
                }
                catch
                {
                    // 如果 COM 失敗，可以進行 binary 解析兜底：
                    try
                    {
                        using (FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.Read))
                        using (BinaryReader fileReader = new BinaryReader(fileStream, Encoding.Default))
                        {
                            // 判斷檔案最小長度
                            if (fileStream.Length < 0x4C) return "";
                            fileStream.Seek(0x14, SeekOrigin.Begin); // 0x14 位置的 flags
                            uint flags = fileReader.ReadUInt32();

                            if ((flags & 1) == 1)
                            {
                                // 需跳過 shell item id list
                                fileStream.Seek(0x4C, SeekOrigin.Begin);
                                // 通常會有一個short來指示item id長度
                                ushort idListLength = fileReader.ReadUInt16();
                                fileStream.Seek(idListLength, SeekOrigin.Current);
                            }

                            long fileInfoStartsAt = fileStream.Position;
                            if (fileInfoStartsAt + 0x10 > fileStream.Length) return "";

                            uint totalStructLength = fileReader.ReadUInt32();
                            fileStream.Seek(0xC, SeekOrigin.Current);
                            uint fileOffset = fileReader.ReadUInt32();

                            long pathPosition = fileInfoStartsAt + fileOffset;
                            if (pathPosition > fileStream.Length) return "";

                            // 設定正確讀取路徑
                            fileStream.Seek(pathPosition, SeekOrigin.Begin);

                            // 再做一層防呆，剩下 bytes 不足就放棄
                            long remaining = (fileInfoStartsAt + totalStructLength) - fileStream.Position - 2;
                            if (remaining < 0 || fileStream.Position + remaining > fileStream.Length || remaining > 4096)
                                return "";

                            // 假設路徑為Unicode
                            byte[] linkTargetBytes = fileReader.ReadBytes((int)remaining);
                            string link = Encoding.Unicode.GetString(linkTargetBytes);
                            int start = link.IndexOf(':');
                            if (start <= 0) return ""; //修正！0或-1都不能用了
                            int end = link.IndexOf('\0', start);
                            if (end < start) end = link.Length;
                            if (start - 1 < 0 || (end - (start - 1)) < 1 || end > link.Length) return "";
                            path = link.Substring(start - 1, end - (start - 1));
                            return path.Trim();
                        }
                    }
                    catch
                    {
                        return "";
                    }
                }
            }
            return "";
        }

        static IconExtractor iconExtractor = new IconExtractor("C:\\WINDOWS\\System32\\shell32.dll");
        private void addAndInitFileCard(int index, string f, DateTime dt = default)
        {
            if (string.IsNullOrEmpty(f))
            {
                return;
            }

            FileDirectoryCard card;
            if (cardList.Count > index)
            {
                cardList[index] = cardList[index].clone();
                card = cardList[index];
            }
            else
            {
                card = new FileDirectoryCard();
                cardList.Add(card);
            }
            card.changedTime = dt;
            card.fullPath2 = "";
            card.type = FileDirectoryCard.Type.file;
            card.onMouseDown = () =>
            {
                runFile(card.fullPath);
            };
            int a = index;
            setRichTextBoxDirectoryPathName(card, a, f);
            bool isImage = false, isVideo = false;
            setCardMouseDown(f, card, ref isImage, ref isVideo);
            card.isImage = isImage;
            card.isVideo = isVideo;
            handleCustomizationImage(f, card);
            if ((isImage || isVideo || card.isIcon) && card.loadImage == null)
            {
                card.loadImage = () =>
                {
                    Bitmap smallerImage = null;
                    string smallerImagePath = null;
                    string path2 = null;
                    bool isCreateNewThumb;
                    if (filePathCacheListManager.imagePathToCacheJPGFile.TryGetValue(f, out var jpeg))
                    {
                        //使用已有縮圖
                        smallerImagePath = path2 = jpeg;
                        isCreateNewThumb = false;
                    }
                    else
                    {
                        //創新縮圖
                        isCreateNewThumb = true;
                        smallerImagePath = requestSmallerPath();
                    }
                    if (imagePathToThumbnailCachePool.ContainsKey(f))
                    {
                        card.setCardImageSafe(imagePathToThumbnailCachePool[f], drawPanel);
                    }
                    else
                    {
                        card.loadingDraw = true;
                        //超LAG區
                        if (isCreateNewThumb)
                        {
                            smallerImage = getSmallCenter(isImage, f, smallerImagePath);
                        }
                        else
                        {
                            smallerImage = LoadImagePure(path2);
                        }

                        if (smallerImage == null)
                        {
                            card.errorImage = true;
                            card.image = null;
                            card.loadingDraw = false;
                        }
                        else
                        {
                            card.setCardImageSafe(smallerImage, drawPanel);
                        }
                        //
                    }
                    ;
                };
            }
            //get extension
            string ext = Path.GetExtension(f).ToLowerInvariant();
            if (ext.Equals(".lnk"))
            {
                string target = CreateShortcut(f);

                if (string.IsNullOrEmpty(target))
                {
                    card.fullPath2 = "break";
                    card.onMouseDown = () =>
                    {
                        go(computerPath);
                    };
                    card.onMouseMiddleDown = () =>
                    {
                        Form1 form1 = getForm1();
                        form1.Show();
                        form1.go(computerPath);
                    };
                }
                else if (File.Exists(target))
                {
                    //runFile(link.TargetPath);
                    setCardMouseDown(target, card, ref isImage, ref isVideo);
                    card.fullPath2 = target;
                }
                else if (Directory.Exists(target))
                {
                    card.onMouseDown = () =>
                    {
                        go(target);
                    };
                    card.onMouseMiddleDown = () =>
                    {
                        Form1 form1 = getForm1();
                        //openList.Add(form1);
                        //form1.BackColor = Color.LightGray;
                        form1.Show();
                        form1.go(target);
                    };
                    card.fullPath2 = target;
                }
                else
                {
                    card.fullPath2 = "break";
                    card.onMouseDown = () =>
                    {
                        go(computerPath);
                    };
                    card.onMouseMiddleDown = () =>
                    {
                        Form1 form1 = getForm1();
                        form1.Show();
                        form1.go(computerPath);
                    };
                }
            }
            else if (ext.Equals(".url") && File.Exists(f))
            {
                string target = CreateShortcut(f);
                setCardMouseDown(target, card, ref isImage, ref isVideo);
                card.fullPath2 = target;
                //runFile(link.TargetPath);
            }
            card.isOther = !isImage && !isVideo;
            if (card.isOther && card.loadImage == null)
            {
                card.loadImage = () =>
                {
                    //if (!File.Exists(f)) return;
                    Bitmap b = null;
                    card.loadingDraw = true;
                    card.isIcon = true;
                    imagePathToThumbnailCachePool.TryGetValue(f, out b);
                    if (b != null)
                    {
                        addImagePathToThumbnailCachePool(ext, b);
                        card.setCardImageSafe(b, drawPanel);
                        setToDraw();
                        return;
                    }
                    if (b == null && filePathCacheListManager.imagePathToCacheJPGFile.TryGetValue(ext, out var thumbPath))
                    {
                        b = getSmallerImage(thumbPath);
                    }
                    if (b == null)
                    {
                        b = IconFunction.GetFileImageFromPath(f, IconFunction.IconSizeEnum.ExtraLargeIcon, true);
                        if (b != null)
                            b = CaptureNonTransparent(b);
                    }
                    if (b == null)
                    {
                        b = Icon.ExtractAssociatedIcon(f).ToBitmap();
                        b = CaptureNonTransparent(b);
                    }
                    if (b == null)
                    {
                        card.loadingDraw = true;
                        return;
                    }
                    addImagePathToThumbnailCachePool(ext, b);
                    card.setCardImageSafe(b, drawPanel);
                    setToDraw();
                    return;
                };
            }
            numToFileDctionaryCard[a] = card;
        }
        public static Bitmap CaptureNonTransparent(Bitmap original)
        {
            int minX = original.Width;
            int minY = original.Height;
            int maxX = 0;
            int maxY = 0;

            // 鎖定Bitmap的位元陣列
            BitmapData bitmapData = original.LockBits(new Rectangle(0, 0, original.Width, original.Height),
                                                      ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            unsafe
            {
                byte* ptr = (byte*)bitmapData.Scan0;

                // 找到非透明部分的邊界
                for (int y = 0; y < original.Height; y++)
                {
                    for (int x = 0; x < original.Width; x++)
                    {
                        byte alpha = ptr[(y * bitmapData.Stride) + (x * 4) + 3];
                        if (alpha != 0)
                        {
                            if (x < minX) minX = x;
                            if (y < minY) minY = y;
                            if (x > maxX) maxX = x;
                            if (y > maxY) maxY = y;
                        }
                    }
                }
            }

            original.UnlockBits(bitmapData);

            // 計算非透明部分的寬度和高度
            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            // 創建新的Bitmap來存放非透明部分
            Bitmap newBitmap = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(newBitmap))
            {
                g.DrawImage(original, new Rectangle(0, 0, width, height), new Rectangle(minX, minY, width, height), GraphicsUnit.Pixel);
            }

            return newBitmap;
        }

        private void handleCustomizationImage(string f, FileDirectoryCard card)
        {
            if (qbSetting.fileToImage.TryGetValue(f, out var fileImage) && fileImage != "")
            {
                card.isCustomizationImageShow = true;
                card.loadingDraw = false;
                string smallerPath = requestSmallerPath();
                card.loadImage = () =>
                {
                    if (File.Exists(fileImage))
                    {
                        card.setCardImageSafe(
                            getSmallerImageAndSave(new Bitmap(fileImage), f, smallerPath), drawPanel);
                    }
                };
            }
            else if (filePathCacheListManager.imagePathToCacheJPGFile.TryGetValue(f, out var path) && path != "")
            {
                card.loadImage = () =>
                {
                    if (File.Exists(path))
                    {
                        card.setCardImageSafe(
                            LoadImagePure(path), drawPanel);
                    }
                };
            }
        }

        /// <summary>
        /// judge type and set mouse down event
        /// </summary>
        private void setCardMouseDown(string f, FileDirectoryCard card, ref bool isImage, ref bool isVideo)
        {
            string ext = Path.GetExtension(f);

            isImage = imageFormat.Contains(ext);
            if (isImage)
            {
                card.onMouseMiddleDown = () =>
                {
                    if (!File.Exists(card.fullPath)) return;
                    pictureBox2.Image = LoadImagePure(card.fullPath);
                    resetPictureBox2();
                };
            }

            isVideo = videoFormat.Contains(ext);
            if (isVideo)
            {
                card.onMouseMiddleDown = () =>
                {
                    if (!File.Exists(card.fullPath)) return;
                    playAndOpenVideo(card.fullPath);
                };
            }

            bool isText = textFormat.Contains(ext);
            if (isText)
            {
                card.onMouseMiddleDown = () =>
                {
                    if (!File.Exists(card.fullPath)) return;
                    richTextBox8.Text = File.ReadAllText(f);
                    richTextBox8.Visible = true;
                    richTextBox8.SelectionStart = 0;
                    richTextBox8.SelectionLength = 0;
                    richTextBox8.ScrollToCaret();
                    richTextBox8.Dock = DockStyle.Fill;
                    richTextBox8.Tag = f;
                };
            }

        }

        private void playAndOpenVideo(string path)
        {
            vlcControl1.Visible = true;
            _mediaPlayer.Stop();
            _mediaPlayer.Play(new Media(_libVLC, new Uri(path)));
            _mediaPlayer.Time = 0;
            progress = 0;
            Task.Delay(500).ContinueWith(t =>
            {
                callMain(() =>
                {
                    first = false;
                });
            });
        }

        void addAndInitCard(int index, string f, DateTime dt = default)
        {
            if (cardList.Count > index)
            {
                switch (cardList[index].type)
                {
                    case FileDirectoryCard.Type.file:
                        addAndInitFileCard(index, f, dt);
                        break;
                    case FileDirectoryCard.Type.directory:
                        addAndInitDirectoryCard(index, f, dt);
                        break;
                    case FileDirectoryCard.Type.disk:
                        addAndInitDisk(index, f, dt);
                        break;
                    default:
                        break;
                }
            }
            else
            {

                if (f.Length <= 3)
                    addAndInitDisk(index, f);
                else if (File.Exists(f))
                    addAndInitFileCard(index, f);
                else if (Directory.Exists(f))
                    addAndInitDirectoryCard(index, f);
            }
        }

        private void addAndInitDisk(int index, string disk, DateTime dt = default)
        {
            FileDirectoryCard card;
            if (index < cardList.Count)
            {
                cardList[index] = cardList[index].clone();
                card = cardList[index];
            }
            else
            {
                card = new FileDirectoryCard();
                cardList.Add(card);
            }
            card.changedTime = dt;
            card.fullPath2 = "";
            //ManualResetEvent w = new ManualResetEvent(false);
            //waitHandles.Add(w);
            numToFileDctionaryCard[index] = card;
            card.type = FileDirectoryCard.Type.disk;
            card.onMouseDown = () =>
            {
                go(card.fullPath);
            };
            card.onMouseMiddleDown = () =>
            {
                Form1 form1 = getForm1();
                //openList.Add(form1);
                form1.Show();
                form1.go(card.fullPath);
            };
            int a = index;
            handleCustomizationImage(disk, card);
            addJob(() =>
            {
                DriveInfo d = new DriveInfo(disk);
                card.index = a;
                card.fileName = $"[{d.VolumeLabel}]\n{disk}";
                card.fullPath = disk;
                //w.Set();
            });
        }
        private int addAndInitDirectoryCard(int index, string f, DateTime dt = default)
        {
            FileDirectoryCard card;
            if (cardList.Count > index)
            {
                var old = cardList[index];
                if (old == null)
                {
                    old = new FileDirectoryCard();
                }
                else
                {
                    cardList[index] = old.clone();
                }
                card = cardList[index];
            }
            else
            {
                card = new FileDirectoryCard();
                cardList.Add(card);
            }
            card.changedTime = dt;
            card.fullPath2 = "";
            card.type = FileDirectoryCard.Type.directory;
            card.isVideo = card.isImage = false;
            handleCustomizationImage(f, card);

            card.onMouseDown = () =>
            {
                go(card.fullPath);
            };
            card.onMouseMiddleDown = () =>
            {
                Form1 form1 = getForm1();
                //openList.Add(form1);
                //form1.BackColor = Color.LightGray;
                form1.Show();
                form1.go(card.fullPath);
            };
            int a = index;
            setRichTextBoxDirectoryPathName(card, a, f);

            numToFileDctionaryCard[a] = card;
            return index;
        }

        public void stopThreads()
        {
            smartThreadPool.Cancel(false);
            smartThreadPool2.Cancel(false);
            smartThreadPool3.Cancel(false);
            smartThreadPool.WaitForIdle();
            smartThreadPool2.WaitForIdle();
            smartThreadPool3.WaitForIdle();
        }
        void setToDraw()
        {
            loadingDraw = false;
            toDraw = true;
            firstDrawIndex = -1;
        }

        bool selecteGoto = true;
        Thread drawThread;
        Thread drawThread2;
        volatile int minIndex, maxIndex;
        volatile bool drawing = true;
        int padding;
        volatile string filterWord;
        volatile string[] filterWordz = new string[0];
        string Z = "";
        int firstDrawIndex = -1;
        volatile int gotoIndex = -1;
        Brush textBackgroundWhite = new SolidBrush(Color.FromArgb(200, Color.White));
        Brush textBackgroundBlack = new SolidBrush(Color.FromArgb(200, Color.Black));
        Brush directoryBackground = new SolidBrush(Color.FromArgb(200, Color.Gray));
        Brush diskBackground = new SolidBrush(Color.FromArgb(200, Color.Azure));

        private static void NOP(double durationSeconds)
        {
            var durationTicks = Math.Round(durationSeconds * Stopwatch.Frequency);
            var sw = Stopwatch.StartNew();

            while (sw.ElapsedTicks < durationTicks)
            {

            }
        }
        private void ZoomDrawImage(Graphics g, Image img, System.Drawing.Rectangle bounds)
        {
            decimal r1 = (decimal)img.Width / img.Height;
            decimal r2 = (decimal)bounds.Width / bounds.Height;
            int w = bounds.Width;
            int h = bounds.Height;
            if (r1 > r2)
            {
                w = bounds.Width;
                h = (int)(w / r1);
            }
            else if (r1 < r2)
            {
                h = bounds.Height;
                w = (int)(r1 * h);
            }
            int x = (bounds.Width - w) / 2;
            int y = (bounds.Height - h) / 2;
            g.DrawImage(img, new Rectangle(x, y, w, h));
        }

        RectangleF backgroundRect
        {
            get
            {
                switch (comboBox1IndexThreadSafe)
                {
                    case 0:
                        if (background != null)
                        {
                            //ZoomDrawImage(g, background, pictureBox1.ClientRectangle);
                            float w = pictureBox1.Height * background.Width / background.Height;
                            float h = pictureBox1.Width * background.Height / background.Width;
                            if (w > pictureBox1.Width)
                            {
                                return new RectangleF((pictureBox1.Width - w) / 2, 0, w, pictureBox1.Height);
                            }
                            else
                            {
                                return new RectangleF(0, (pictureBox1.Height - h) / 2, pictureBox1.Width, h);
                            }
                        }
                        if (background != null)
                        {
                            float w, h;
                            w = background.Width;
                            h = background.Height;
                            if (w < pictureBox1.Width)
                            {
                                float big = pictureBox1.Width / (float)w;
                                w = pictureBox1.Width;
                                h *= big;
                            }

                            if (h < pictureBox1.Height)
                            {
                                float big = pictureBox1.Height / (float)h;
                                h *= big;
                                w *= big;
                            }

                            w = (float)Math.Ceiling(w);
                            h = (float)Math.Ceiling(h);
                            return new RectangleF((-(w - pictureBox1.Width) / 2),
                                (-(h - pictureBox1.Height) / 2), w, h);
                        }

                        break;
                    case 1:
                        if (background != null)
                        {
                            return new RectangleF((pictureBox1.Width - bitmapWidth) / 2, (pictureBox1.Height - bitmapHeight) / 2, bitmapWidth, bitmapHeight);

                        }
                        break;
                    case 2:
                        if (background != null)
                        {
                            return new RectangleF((pictureBox1.Width - background.Width) / 2, (pictureBox1.Height - background.Height) / 2, background.Width, background.Height);
                        }
                        break;
                }
                return new RectangleF(0, 0, pictureBox1.Width, pictureBox1.Height);
            }
        }
        ConcurrentQueue<Action> job1Queue = new ConcurrentQueue<Action>();
        ConcurrentQueue<Action> job2Queue = new ConcurrentQueue<Action>();
        ConcurrentQueue<Action> job3Queue = new ConcurrentQueue<Action>();
        volatile private int comboBox1IndexThreadSafe;
        DateTime clearTime;
        DateTime loadImageTime = DateTime.Now;
        DateTime loadTextTime = DateTime.Now;
        int loadImageCount = 5;
        ConcurrentDictionary<string, Bitmap> pathToRectIcon = new ConcurrentDictionary<string, Bitmap>();
        private volatile bool _loadingDraw;
        private bool loadingDraw
        {
            set => _loadingDraw = value;
            get => _loadingDraw;
        }
        Brush FromARGB(float ax, float rx, float gx, float bx)
        {
            return new SolidBrush(Color.FromArgb((int)(ax * 255), (int)(rx * 255), (int)(gx * 255), (int)(bx * 255)));
        }
        Brush FromARGB(float ax, int rx, int gx, int bx)
        {
            return new SolidBrush(Color.FromArgb((int)(ax * 255), rx, gx, bx));
        }
        static Random random = new Random();
        ConcurrentDictionary<Image, int> imageTimer = new ConcurrentDictionary<Image, int>();
        volatile int timer = 0;
        static Font font;
        static Font font2;
        void insideDraw()
        {
            if (resetingGraphic)
            {
                while (resetingGraphic)
                {
                    waitingDrawing = true;
                    Thread.Sleep(1);
                }
                waitingDrawing = false;
            }
            int paddingX = 45;
            float maxHeight = 0;
            double scaleRec = 1;
            Color clearColor = Color.Empty;
            Pen selectBoxPen = new Pen(Color.White, 4);
            if (font == null || font2 == null)
            {
                font = new Font(Font.FontFamily, Math.Max(10, (Font.Size + 10) * scale));
                font2 = new Font(Font.FontFamily, Math.Max(10, (Font.Size) * scale));
            }
            //g.Clear(clearColor);
            drawBackground(g);
            if (loadingDraw || waitAllDrawOK)
            {
                timer++;
                if (timer > 500)
                {
                    waitLoadingAnimation(g);
                }
                else
                {
                }
                Thread.Sleep(1);
                return;
            }
            else
            {

                previousTime = DateTime.Now.Millisecond;
                timer = 0;
            }

        drawStart:
            maxHeight = 0;
            toDraw = false;
            Rectangle r = new Rectangle();
            int tempRecoedIndex = -1;
            r.Width = (int)((picW + 10) * scale);
            r.Height = (int)((picH + 30) * scale);
            padding = ((bitmapWidth - paddingX) % (r.Width)) / 2;
            r.X = padding;
            if (comboBox1IndexThreadSafe == 1)
            {
                r.X += (int)backgroundRect.X;
            }
            if (gotoIndex != -1)
            {
                r.Y = (int)startTargetScrollY;
            }
            else
            {
                r.Y = (int)scrollY + padding;
            }

            minIndex = -1;
            bool hasImageLine = false;
            int wordBoxHeight = 30;
            int firstXIndex = 0;
            if (loadAllCard && cardList.Count == 0)
            {
                const string label = "No File And Directory";

                var size = g.MeasureString(label, Font, new Size(100, 50));
                g.DrawString(label, Font, Brushes.Black, pictureBox1.Width / 2 - size.Width / 2,
                    pictureBox1.Height / 2 - size.Height / 2);
            }

            bool justBackground = false;
            for (int i = 0; i < cardList.Count; i++)
            {
                FileDirectoryCard card = cardList[i];
                if (card == null) continue;
                bool selectjump = !string.IsNullOrEmpty(selectName);
                bool jump = false;
                if (!string.IsNullOrEmpty(filterWord) && card.fileName != null && !global)
                {
                    for (int j = 0; j < filterWordz.Length; j++)
                    {
                        string f = filterWordz[j];
                        jump |= card.fileName.IndexOf(f, StringComparison.OrdinalIgnoreCase) == -1;
                    }
                }

                if (selectjump && card.fileName == null)
                {
                    card.fileName = Path.GetFileName(card.fullPath);
                }

                if (!string.IsNullOrEmpty(selectName))
                {
                    justBackground = true;
                }
                if ((card.fileName != null && card.fileName.Equals(selectName)) || i == gotoIndex)
                {
                    if (triggerNoSelectGoTo)
                    {
                        targetScrollY = scrollY = -r.Y - padding;
                        if (scrollY > 0)
                        {
                            targetScrollY = scrollY = startTargetScrollY;
                        }
                        triggerNoSelectGoTo = false;
                    }
                    else
                    {
                        targetScrollY = scrollY = -r.Y + r.Height / 2 + bitmapHeight / 4 + padding;
                        if (scrollY > 0)
                        {
                            targetScrollY = scrollY = startTargetScrollY;
                        }

                        clearSelectedAndHandleUI();
                        addSelectedCard(card);

                        var card2 = card;
                        lock (card2)
                        {
                            callMain(() =>
                            {
                                if (qbSetting.fileToImage.ContainsKey(card2.fullPath))
                                {
                                    richTextBox7.Text = qbSetting.fileToImage[card2.fullPath];
                                }
                                else
                                {
                                    richTextBox7.Text = "";
                                }
                            });
                        }
                    }

                    selectName = null;
                    gotoIndex = -1;
                    goto drawStart;
                }
                if ((card.isIcon || card.isImage || card.isVideo || card.isOther || card.isCustomizationImageShow) &&
                    card.needDoImageThings(mode))
                {
                    r.Height = picH + wordBoxHeight + 15;
                }
                else
                {
                    r.Height = picH + wordBoxHeight + 15;
                    //r.Height = picH / 3 + wordBoxHeight + 15;
                }

                r.Height = (int)(r.Height * scale);
                if (r.Height > maxHeight)
                {
                    maxHeight = r.Height;
                }

                if (jump && (gotoIndex == -1))
                {
                    card.bound = Rectangle.Empty;
                    continue;
                }

                if (card.index == -1)
                {
                    card.bound = Rectangle.Empty;
                    continue;
                }
                card.bound = r;
                //card.bound.X += (pictureBox1.Width - bitmapWidth) / 2;
                //card.bound.Y += (pictureBox1.Height - bitmapHeight) / 2;
                RectangleF r2 = r;
                //r2.Width = 50;
                //r2.Height = 50;
                r2.X = r.X;
                r2.Y = r.Y;
                RectangleF r4 = r;
                r4.Inflate(-5f, -5f);
                if (gotoIndex != -1 || !string.IsNullOrEmpty(selectName))
                {
                    if (r.Y + r.Height > 0)
                    {
                        if (firstDrawIndex == -1)
                        {
                            firstDrawIndex = i;
                            tempRecoedIndex = firstDrawIndex;
                        }

                        if (minIndex == -1)
                        {
                            minIndex = i;
                        }

                        maxIndex = i;
                    }

                    goto NoDraw;
                }

                if (r.Y + r.Height > 0)
                {
                    if (firstDrawIndex == -1)
                    {
                        firstDrawIndex = i;
                        if (i != -1)
                        {
                            firstDrawIndex = i;
                            tempRecoedIndex = firstDrawIndex;
                        }
                    }

                    if ((card.image != null || card.loadImage != null) && card.needDoImageThings(mode))
                    {
                        hasImageLine = true;
                    }

                    if (!card.errorImage && card.image == null && card.loadImage != null && !card.loadingDraw)
                    {
                        if (card.needDoImageThings(mode))
                        {
                            if (drawed.Contains(card.fullPath))
                            {
                                card.loadingDraw = true;
                                if (card.loadImage != null)
                                {
                                    addJob2(() =>
                                    {
                                        card.loadImage();
                                    });
                                }
                            }
                            else if (DateTime.Now >= loadImageTime && loadImageCount > 0)
                            {
                                card.loadingDraw = true;
                                if (card.loadImage != null)
                                {
                                    addJob2(() =>
                                    {
                                        card.loadImage();
                                    });
                                }
                                loadImageCount--;
                                if (loadImageCount <= 0)
                                {
                                    loadImageTime = DateTime.Now.AddMilliseconds(1);
                                    loadImageCount = 20;
                                }
                                drawed.Add(card.fullPath);
                            }
                        }
                    }

                    if (minIndex == -1)
                    {
                        minIndex = i;
                    }

                    maxIndex = i;


                    Rectangle rUp = new Rectangle(r.X, r.Y, r.Width, wordBoxHeight + 10);
                    switch (card.type)
                    {
                        case FileDirectoryCard.Type.file:
                            break;
                        case FileDirectoryCard.Type.directory:
                            break;
                        default:
                            break;
                    }

                    #region 背景跟檔案圖一起畫

                    RectangleF rBlock = r4;
                    if (card.image == null && card.loadImage == null)
                    {
                        card.image = FormMain.folder;
                    }
                    if (card.image != null)
                    {
                        int w, h;
                        bool sameSize = card.isCustomizationImageShow || !card.isOther;
                        if (!sameSize)
                        {
                            sameSize = (card.imageWidth < 256 && card.imageHeight < 256);
                        }

                        float wh = (float)card.imageWidth / card.imageHeight;
                        if (wh > 1)
                        {
                            w = (int)(picW * scale);
                            h = (int)(picW / wh * scale);
                        }
                        else
                        {
                            w = (int)(picH * wh * scale);
                            h = (int)(picH * scale);
                        }
                        float spaceX = r4.Width - w;
                        float spaceY = r4.Height - wordBoxHeight - h - 25;
                        rBlock.Width = w;
                        rBlock.Height = h;
                        rBlock.X += spaceX / 2;
                        rBlock.Y += spaceY / 2 + 10;
                        while (rBlock.Right >= r.Right - 40 || rBlock.Top <= r.Top - 80)
                        {
                            rBlock.X += (int)(rBlock.Width * 0.05f);
                            rBlock.Y += (int)(rBlock.Height * 0.05f);
                            rBlock.Width *= 0.9f;
                            rBlock.Height *= 0.9f;
                        }
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                        g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        if (card?.image != null)
                        {
                            g.DrawImage(card.image2, rBlock);
                        }
                    }
                    #endregion

                    RectangleF r5 = r4;
                    r5.Y += 100;
                    r5.Height = 60;
                    string indexStr = card.index.ToString();
                    SizeF sizeF = g.MeasureString(indexStr, font2);
                    switch (card.type)
                    {
                        case FileDirectoryCard.Type.file:
                            drawShineString(g, card.fileName, font2, r5, Color.Blue, Color.Gray, 3);
                            break;
                        case FileDirectoryCard.Type.directory:
                            drawShineString(g, card.fileName, font2, r5, Color.Black, Color.LightGray, 3);
                            break;
                        case FileDirectoryCard.Type.disk:
                            drawShineString(g, card.fileName, font2, r5, Color.Purple, Color.Yellow, 3);
                            break;
                        default:
                            break;
                    }

                    if (selectRectangleMode)
                    {
                        g.DrawRectangle(Pens.Gray, selectRectangle.X, selectRectangle.Y, selectRectangle.Width,
                            selectRectangle.Height);
                    }
                    if (card.isSelected)
                    {

                        //g.FillRectangle(Brushes.LightGray, r);
                        RectangleF rr = r4;
                        //rr.Inflate(new Size(4, 4));
                        g.DrawRectangle(selectBoxPen, rr.X - 4f, rr.Y - 4f, rr.Width + 8f, rr.Height + 8f);
                    }
                    switch (card.type)
                    {
                        case FileDirectoryCard.Type.file:
                            //g.FillRectangle(textBackgroundWhite, r);
                            break;
                        case FileDirectoryCard.Type.directory:
                            //g.FillRectangle(directoryBackground, r);
                            break;
                        case FileDirectoryCard.Type.disk:
                            //g.FillRectangle(diskBackground, r);

                            //容量
                            g.FillRectangle(FromARGB(0.5f, 0, 0.5f, 0.5f), new RectangleF(r.Left + 5, r.Bottom - 5, r.Width - 10, 20));

                            float percent = (float)card.size / card.getFullDiskSize;
                            g.FillRectangle(FromARGB(1, 0, 1, 0.5f), new RectangleF(r.Left + 5, r.Bottom - 5, (r.Width - 10) * percent, 20));

                            string text = $"{FileSizeFormatter.FormatSize(card.size)}/{FileSizeFormatter.FormatSize(card.getFullDiskSize)} ({percent * 100:0.0}%)";
                            var bound = g.MeasureString(text, font2);
                            var w = (r.Width - bound.Width) / 4;
                            drawShineString(g, text, font2, new RectangleF(r.Left + w, r.Bottom - 5, r.Width - w * 2, 20), Color.Green, Color.Green, 3);
                            break;
                        default:
                            break;
                    }

                    //drawShineString(indexStr, font, Brushes.Blue, r2.Location, Color.White, 3);
                    /*r2.Offset(-1, -1);
                    g.DrawString(indexStr, font, Brushes.White, r2.Location);
                    r2.Offset(+1, +1);*/
                    var sizeBigNum = g.MeasureString(indexStr, font);
                    sizeBigNum.Width += 5;
                    sizeBigNum.Height += 5;
                    //g.FillRectangle(Brushes.Black, r2.X, r2.Bottom - sizeBigNum.Height, sizeBigNum.Width + 10, sizeBigNum.Height);
                    //g.DrawText(indexStr,Color.Blue,  font,  r2);
                    RectangleF indexR2 = r2;
                    indexR2.Y = r2.Bottom - sizeBigNum.Height;
                    indexR2.X += 5;
                    indexR2.Width += 10;
                    //drawShineString(g, indexStr, font, indexR2, Color.Blue, 2);

                }

            NoDraw:
                if (card.image != null)
                {
                    hasImageLine = true;
                }

                r.X += r.Width + 5;
                int moreHeightSpace = 5;
                if (r.X + r.Width + 5 >= blockRight)
                {
                    r.X = padding;
                    if (comboBox1IndexThreadSafe == 1)
                    {
                        r.X += (int)backgroundRect.X;
                    }
                    firstXIndex = i + 1;
                    //r.Y += picH + 35;
                    r.Y += (int)(maxHeight + moreHeightSpace);
                    if (isDisk)
                    {
                        r.Y += 5;
                    }
                    maxHeight = 0;
                    const int preloadHeight = 300;
                    if (r.Y >= pictureBox1.Height + preloadHeight && gotoIndex == -1 &&
                        string.IsNullOrEmpty(selectName))
                    {
                        break;
                    }

                    hasImageLine = false;
                }

                max = r.Y;
            }

            if (gotoIndex != -1 || selectName != "")
            {
                selectName = "";
                gotoIndex = -1;
            }

            if (tempRecoedIndex != -1)
            {
                recoredFirstIndex = tempRecoedIndex;
                //Debug.WriteLine(recoredFirstIndex);
            }

            float scrollHeight = getScrollHeight();
            if (scrollHeight >= bitmapHeight)
            {
                g.FillRectangle(Brushes.Gray, new RectangleF(blockRight - 20 - 1, 20 - 1, 3, bitmapHeight - 40));
                g.DrawLine(Pens.White, new PointF(blockRight - 20, 20),
                    new PointF(blockRight - 20, bitmapHeight - 20));
                g.FillEllipse(Brushes.Gray,
                    new RectangleF(blockRight - 30 - 2,
                        20 - (((float)scrollY / getScrollHeight()) * (bitmapHeight - 40)) - 2, 24, 24));
                g.FillEllipse(Brushes.White,
                    new RectangleF(blockRight - 30,
                        20 - (((float)scrollY / getScrollHeight()) * (bitmapHeight - 40)), 20, 20));
            }

            if (justBackground)
            {
                g.Clear(Color.Black);
                drawBackground(g);
            }
        }
        int previousTime = 0;
        private void waitLoadingAnimation(Graphics g)
        {
            double scaleRec;
            scaleRec = Math.Abs(Math.Sin((DateTime.Now.Millisecond - previousTime + 1) * 0.001f * Math.PI));
            int s = (int)(50 * scaleRec);
            Rectangle whiteRec = new Rectangle((int)(workWidth / 2 - s / 2),
                (int)(workHeight / 2 - s / 2), s, s);
            Rectangle outsideRec, insideRec;
            outsideRec = whiteRec;
            outsideRec.X -= 1;
            outsideRec.Y -= 1;
            outsideRec.Width += 2;
            outsideRec.Height += 2;
            insideRec = whiteRec;
            insideRec.X += 1;
            insideRec.Y += 1;
            insideRec.Width -= 2;
            insideRec.Height -= 2;
            g.DrawRectangle(Pens.AliceBlue, insideRec);
            g.DrawRectangle(Pens.AliceBlue, outsideRec);
            g.DrawRectangle(Pens.AliceBlue, whiteRec);
        }
        private bool exit = false;
        private Action refresh;

        bool focusing;
        bool pausing => (WindowState == FormWindowState.Minimized || !Visible || !focusing) && !exit;
        private bool going = false;
        void drawingThread()
        {
            while (!exit)
            {
                while (pausing)
                {
                    Thread.Sleep(100);
                }
                try
                {
                    insideDraw();
                    var ia = drawPanel.BeginInvoke(refresh);
                    Thread.Sleep(10);
                    drawPanel.EndInvoke(ia);
                }
                catch (Exception ex)
                {
                    System.Media.SystemSounds.Hand.Play();
                    Debug.WriteLine(ex.ToString());
                }
            }
        }

        private float getScrollHeight()
        {
            int ww = (bitmapWidth / ((int)((picW + 10) * scale) + 5));
            float hh = ((float)cardList.Count / ww) - 2;
            //return Math.Max(1, (hh * (int)(picH + wordBoxHeight + 15 + moreHeightSpace)));
            return Math.Max(1, (hh * (int)(picH + 30 + 15 + 5)));
        }

        private void drawBackground(Graphics g)
        {
            if (background != null)
            {
                lock (background)
                {
                    if (background != null)
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                        g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                        g.DrawImage(background, backgroundRect);
                    }
                }
            }
            else
            {
                g.Clear(Color.Black);
            }
        }

        Action refreshPictureBox;
        DateTime drawTime = DateTime.Now;
        int drawTextCount = 10;
        private ConcurrentDictionary<(string, Color, Color), Bitmap> textToImage = new ConcurrentDictionary<(string, Color, Color), Bitmap>();
        static StringBuilder drawSB = new StringBuilder();
        //g.DrawString(card.fileName, Font, Brushes.White, r4);
        static ConcurrentDictionary<Color, Brush> C2B = new ConcurrentDictionary<Color, Brush>();
        void drawShineString(Graphics g, string text, Font f, RectangleF rec, Color shineColor, Color backColor, float shineSize = 2)
        {
            if (string.IsNullOrEmpty(text)) return;
            var key = (text, shineColor, backColor);
            if (!textToImage.TryGetValue(key, out var image3) && DateTime.Now >= loadImageTime && loadImageCount > 0)
            {
                image3 = textToImage[key] = null;
                addJob(() =>
                {
                    var bmpGraphics3 = new Bitmap((int)rec.Width, (int)rec.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    Graphics g3 = Graphics.FromImage(bmpGraphics3);
                    //g3.Clear(backColor);
                    //g3.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    g3.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                    g3.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                    g3.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                    var size = g3.MeasureString(text, f, (int)(rec.Width));
                    float baseX = rec.Width * 0.5f - size.Width * .5f;

                    Brush brush = new SolidBrush(Color.FromArgb(10, shineColor.R, shineColor.G, shineColor.B));
                    string text2 = text;
                    const int maxCardNameLength = 40;
                    var maxLength = Math.Min(maxCardNameLength, text.Length);
                    if (size.Height >= 40)
                    {
                        while (true)
                        {
                            if (maxLength < 10 || size.Height < 40)
                            {
                                text2 = text.Substring(0, (int)maxLength) + "...";
                                break;
                            }
                            text2 = text.Substring(0, (int)maxLength) + "...";
                            size = g3.MeasureString(text2, f, (int)(rec.Width - baseX));
                            maxLength--;
                        }
                    }

                    for (float y = -shineSize; y <= shineSize; y += 0.5f)
                    {
                        for (float x = -shineSize; x <= shineSize; x += 0.5f)
                        {
                            g3.DrawString(text2, f, brush, new RectangleF(1 + x + baseX, 1 + y, rec.Width - baseX, rec.Height));
                        }
                    }
                    brush.Dispose();
                    g3.DrawString(text2, f, Brushes.White, new RectangleF(1 + 0.5f + baseX, 1, rec.Width - baseX, rec.Height));
                    g3.DrawString(text2, f, Brushes.White, new RectangleF(1 - 0.5f + baseX, 1, rec.Width - baseX, rec.Height));
                    g3.DrawString(text2, f, Brushes.White, new RectangleF(1 + baseX, 1 + 0.5f, rec.Width - baseX, rec.Height));
                    g3.DrawString(text2, f, Brushes.White, new RectangleF(1 + baseX, 1 - 0.5f, rec.Width - baseX, rec.Height));
                    image3 = textToImage[key] = bmpGraphics3;
                    g3.Dispose();

                    loadImageCount--;
                    if (loadImageCount <= 0)
                    {
                        loadImageTime = DateTime.Now.AddMilliseconds(10);
                        loadImageCount = 5;
                    }
                });
            }
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SystemDefault;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
            if (image3 == null)
            {
                if (!C2B.TryGetValue(backColor, out Brush brush))
                {
                    brush = C2B[backColor] = new SolidBrush(backColor);
                }
                //g.FillRectangle(brush, rec);
                return;
            }
            g.DrawImageUnscaledAndClipped(image3, Rectangle.Ceiling(rec));
        }
        void drawShineString(Graphics g, string text, Font f, PointF p, Color shineColor, float shineSize = 2)
        {
            float dd = (float)(shineSize * Math.Sqrt(2));
            for (float y = p.Y - shineSize; y <= p.Y + shineSize; y += 0.5f)
            {
                for (float x = p.X - shineSize; x <= p.X + shineSize; x += 0.5f)
                {
                    //float alpha = (float)((dd - Math.Sqrt(Math.Pow(Math.Abs(y - p.Y), 2) + Math.Pow(Math.Abs(x - p.X), 2))) / dd);
                    float alpha = 0.01f;
                    Color color = Color.FromArgb((int)(alpha * 255), shineColor);
                    //Color c = Color.FromArgb(30, shineColor);
                    //g.DrawText(text, f, new SolidBrush(c), p2, StringFormat.GenericTypographic);
                    g.DrawString(text, Font, new SolidBrush(color), x, y);
                }
            }
            //g.DrawText(text, f, wordBrush, p, StringFormat.GenericTypographic);
            g.DrawString(text, Font, Brushes.White, p.X, p.Y);

        }
        volatile bool toDraw = false, toClearAndLoad = false;
        bool needDraw(int i)
        {
            return i >= minIndex && i <= maxIndex;
        }
        void freeBitmap()
        {
            Image image;
            while (waitDispose.Count > 0)
            {
                waitDispose.TryDequeue(out image);
                image.Dispose();
            }
        }
        const int picW = 150, picH = 100;
        private Bitmap getSmallerImage(string smallerPath)
        {
            Bitmap smallerImage;
            Bitmap bOrigin = LoadImagePure(smallerPath);
            int w, h;
            w = bOrigin.Width;
            h = bOrigin.Height;
            if (w > picW)
            {
                //寬
                w = picW;
                h = (int)(bOrigin.Height * picW / (float)bOrigin.Width);
            }
            if (h > picH)
            {
                //寬
                h = picH;
                w = (int)(bOrigin.Width * picH / (float)bOrigin.Height);
            }
            bOrigin = CaptureNonTransparent(bOrigin);
            smallerImage = new Bitmap(w, h);
            Graphics g = Graphics.FromImage(smallerImage);
            g.DrawImage(bOrigin, 0, 0, w, h);
            g.Dispose();
            bOrigin.Dispose();
            return smallerImage;
        }

        private Bitmap getSmallerImageAndSave(Bitmap image, string imagePath, string smallerImagePath)
        {
            if (image == null)
            {
                return null;
            }
            Bitmap smallerImage;
            Bitmap bOrigin = image;
            int w, h;
            w = bOrigin.Width;
            h = bOrigin.Height;
            if (w > picW)
            {
                //寬
                w = picW;
                h = (int)(bOrigin.Height * picW / (float)bOrigin.Width);
            }
            if (h > picH)
            {
                //寬
                h = picH;
                w = (int)(bOrigin.Width * picH / (float)bOrigin.Height);
            }

            if (w <= 0 || h <= 0) return null;
            smallerImage = new Bitmap(w, h);
            Graphics g = Graphics.FromImage(smallerImage);
            g.DrawImage(bOrigin, 0, 0, w, h);
            g.Dispose();
            Bitmap b2 = new Bitmap(w, h);
            Graphics g2 = Graphics.FromImage(b2);
            g2.DrawImage(smallerImage, 0, 0, w, h);
            g2.Dispose();
            saveImage(b2, smallerImagePath);
            filePathCacheListManager.imagePathToCacheJPGFile[imagePath] = smallerImagePath;
            b2.Dispose();
            bOrigin.Dispose();
            addImagePathToThumbnailCachePool(imagePath, smallerImage);
            return smallerImage;
        }

        private Bitmap getSmallerImageAndSave(string imagePath, string smallerImagePath)
        {
            if (smallerImagePath == null)
            {
                return getSmallerImage(smallerImagePath);
            }

            return getSmallerImageAndSave(LoadImagePure(imagePath), imagePath, smallerImagePath);

        }
        public Bitmap GetThumbnail(string videoPath, string thumbnailPath)
        {
            if (filePathCacheListManager.imagePathToEditTime.ContainsKey(videoPath))
            {
                return null;
            }
            var thumbImage = FFmpegThumbnailer.GetThumbnailFromVideo(videoPath, 5);
            if (thumbImage != null)
            {
                filePathCacheListManager.imagePathToEditTime[videoPath] = DateTime.Now;
                addJobFree(() =>
                {
                    thumbImage.Save(thumbnailPath, System.Drawing.Imaging.ImageFormat.Jpeg);
                    filePathCacheListManager.imagePathToCacheJPGFile[videoPath] = thumbnailPath;
                    filePathCacheListManager.JPGFileToimagePath[thumbnailPath] = videoPath;
                    addImagePathToThumbnailCachePool(videoPath, thumbImage);
                });
            }
            return thumbImage;
        }
        void addImagePathToThumbnailCachePool(string filePath, Bitmap thumbnailImage)
        {
            cacheBitmapPixelCount += (thumbnailImage.Width * thumbnailImage.Height);

            while (cacheBitmapPixelCount > cacheBitmapPixelMaxCount)
            {
                Bitmap b2;
                foreach (var item in imagePathToThumbnailCachePool)
                {
                    imagePathToThumbnailCachePool.TryRemove(item.Key, out b2);
                    cacheBitmapPixelCount -= (b2.Width * b2.Height);
                    break;
                }
            }
            imagePathToThumbnailCachePool[filePath] = thumbnailImage;
        }
        public byte[] ReadAllBytes(string fileName)
        {
            byte[] fileData = null;
            FileStream fs = File.OpenRead(fileName);
            using (BinaryReader binaryReader = new BinaryReader(fs))
            {
                fileData = binaryReader.ReadBytes((int)fs.Length);
            }
            fs.Close();
            return fileData;
        }
        static ImageConverter imageConverter = new ImageConverter();
        volatile ConcurrentDictionary<string, Bitmap> imagePathToThumbnailCachePool = new ConcurrentDictionary<string, Bitmap>();
        volatile List<string> list = new List<string>();
        int cacheBitmapPixelCount = 0, cacheBitmapPixelMaxCount = 1024 * 1024 * 512;
        Bitmap LoadImage(string path)
        {
            byte[] bytes = ReadAllBytes(path);
            Bitmap bitmap = (Bitmap)(imageConverter).ConvertFrom(bytes);
            return bitmap;
        }
        static volatile ConcurrentDictionary<string, byte[]> pathToBitmap = new ConcurrentDictionary<string, byte[]>();
        Bitmap LoadImagePure(string path)
        {
            byte[] bytes;
            if (!pathToBitmap.TryGetValue(path, out bytes))
            {
                bytes = ReadAllBytes(path);
                pathToBitmap[path] = bytes;
            }
            Bitmap bitmap;
            if (bytes == null || bytes.Length == 0) return null;
            try
            {
                bitmap = (Bitmap)(imageConverter).ConvertFrom(bytes);
                return bitmap;
            }
            catch (Exception ex)
            {
                return null;
            }
        }
        void setPathImage(string path, Bitmap bitmap)
        {
            imagePathToThumbnailCachePool[path] = bitmap;
        }
        void setRichTextBoxDirectoryPathName(FileDirectoryCard card, int index, string path)
        {
            card.index = index;
            card.fileName = Path.GetFileName(path);
            card.fullPath = path;
        }
        volatile string selectName = "";
        Mode mode;
        float oneScrollMoveDelta = 1.28f;

        private string maxLengthPath = "";
        void back()
        {
            string path = richTextBox1.Text;
            if (path == computerPath) return;

            addJobFree(() =>
            {
                waitAllDrawOK = true;
                loadingDraw = true;

                saveNowState();

                callMain(() =>
                {
                    path = richTextBox1.Text;
                    label3.Text = consoleDigital = "";
                });
                selecteGoto = true;
                selectName = Path.GetFileName(path);
                if (selectName == path)
                {
                    return;
                }
                string parent = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parent))
                {
                    path = parent;
                }
                else
                {
                    path = computerPath;
                }
                callMain(() => { richTextBox5.Text = ""; });
                gotoIndex = -1;
                go(path);
            });
        }

        void forward()
        {
            string path = richTextBox1.Text;
            addJobFree(() =>
            {
                waitAllDrawOK = true;
                loadingDraw = true;

                saveNowState();
                callMain(() =>
                {
                    label3.Text = consoleDigital = "";
                });
                selecteGoto = true;
                if (path == computerPath)
                {
                    path = maxLengthPath.Substring(0, 3);
                }
                else
                {
                    string nowPath = getNowPath();
                    if (nowPath.Length + 1 <= maxLengthPath.Length)
                    {
                        int find = maxLengthPath.IndexOf('\\', nowPath.Length + 1);
                        if (find != -1)
                        {
                            path = maxLengthPath.Substring(0, find); ;
                        }
                        else
                        {
                            path = maxLengthPath;
                        }
                    }
                    else
                    {
                        path = maxLengthPath;
                    }
                }

                if (path == nowPath) return;

                callMain(() => { richTextBox5.Text = ""; });
                gotoIndex = -1;
                go(path);
            });
        }

        private int histroyIndex = 0;
        void older()
        {
            saveNowState();
            moveHistroy(-1);
        }

        private bool histroyMode = true;
        void moveHistroy(int offset)
        {
            if (history.Count == 0) return;
            label3.Text = consoleDigital = "";
            int oldHistroyIndex = histroyIndex;
            if (histroyIndex < 0) histroyIndex = 0;
            else if (histroyIndex >= history.Count) histroyIndex = history.Count - 1;
            histroyIndex += offset;
            if (histroyIndex < 0) histroyIndex = 0;
            else if (histroyIndex >= history.Count) histroyIndex = history.Count - 1;

            if (oldHistroyIndex == histroyIndex) return;

            histroyMode = true;
            string to;
            to = history[histroyIndex];

            //loadNowState();
            addJobFree(() =>
            {
                if (to.StartsWith("s:"))
                {
                    to = to.Substring(2);
                    goGlobal(to);
                }
                else if (to == computerPath || Directory.Exists(to) || File.Exists(to))
                {
                    goOrGlobalGo(to);
                }
                else if (histroyIndex >= 1 && histroyIndex < history.Count - 1)
                {
                    moveHistroy(offset);
                }
            });
        }
        void reOlder()
        {
            saveNowState();
            moveHistroy(+1);
        }
        bool lockGlobal;
        bool global;
        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.L)
            {
                if (pictureBox2.Visible)
                {
                    timer3.Interval = (int)numericUpDown1.Value;
                    timer3.Enabled = !timer3.Enabled;
                }

                return;
            }
            if (e.KeyCode == Keys.F11)
            {
                lockGlobal = !lockGlobal;
            }
            if (e.KeyCode == Keys.F12)
            {
                //Enabled = false;
                loadingDraw = true;
                lockPictureBoxMouswDown = true;
                if (pictureBox1.Dock == DockStyle.Fill)
                {
                    FormBorderStyle = FormBorderStyle.Sizable;
                    WindowState = FormWindowState.Normal;
                    pictureBox1.Dock = DockStyle.None;
                    pictureBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
                    //pictureBox1.BorderStyle = BorderStyle.None;
                    padding = 20;
                    pictureBox1.Width = Width - pictureBoxWidthLess;
                }
                else if (WindowState == FormWindowState.Maximized)
                {
                    WindowState = FormWindowState.Normal;
                    //pictureBox1.BorderStyle = BorderStyle.None;
                    FormBorderStyle = FormBorderStyle.None;
                    pictureBox1.Dock = DockStyle.Fill;
                    padding = 5;
                    WindowState = FormWindowState.Maximized;
                }
                else //Normal
                {
                    if (pictureBox1.Dock == DockStyle.Fill)
                    {
                        pictureBox1.BorderStyle = BorderStyle.None;
                        FormBorderStyle = FormBorderStyle.None;
                        WindowState = FormWindowState.Maximized;
                        pictureBox1.Dock = DockStyle.Fill;
                        padding = 5;
                    }
                    else
                    {
                        FormBorderStyle = FormBorderStyle.Sizable;
                        WindowState = FormWindowState.Maximized;
                        pictureBox1.Dock = DockStyle.None;
                    }
                }
                resetPictureBoxMain();
                //Enabled = true;
                lockPictureBoxMouswDown = false;
                loadingDraw = false;
                return;
            }
            if (e.KeyCode == Keys.J && e.Control)
            {
                button6_Click(sender, e);
            }
            if (richTextBox1.Focused || richTextBox3.Focused || richTextBox5.Focused || richTextBox6.Focused || richTextBox7.Focused)
            {
                return;
            }

            switch (e.KeyCode)
            {
                case Keys.A:
                    if (e.Control)
                    {
                        lock (selected)
                        {
                            selected.Clear();
                            if (string.IsNullOrEmpty(filterWord))
                            {
                                Parallel.ForEach(cardList, (card) =>
                                {
                                    card.isSelected = true;
                                    selected.Add(card);
                                });
                            }
                            else
                            {
                                Parallel.ForEach(cardList, (card) =>
                                {
                                    bool inFilter = false;
                                    if (card.fileName == null)
                                    {
                                        card.fileName = Path.GetFileName(card.fullPath);
                                    }

                                    for (int j = 0; j < filterWordz.Length; j++)
                                    {
                                        string f = filterWordz[j];
                                        inFilter = card.fileName.IndexOf(f, StringComparison.OrdinalIgnoreCase) != -1;
                                        if (inFilter)
                                        {
                                            break;
                                        }
                                    }

                                    if (inFilter)
                                    {
                                        card.isSelected = true;
                                        selected.Add(card);
                                    }
                                });
                            }
                        }

                        setToDraw();
                    }

                    break;
                case Keys.N:
                    if (e.Control)
                    {
                        var newForm1 = getForm1();
                        //newForm1.BackColor = Color.LightGray;
                        newForm1.Show();
                        newForm1.go(Form1.qbSetting.home);
                    }

                    break;
                case Keys.Delete:
                    button5_Click(sender, e);
                    e.Handled = true;
                    break;
                case Keys.Left:
                    if (e.Control)
                    {
                        back();
                    }
                    else
                    {
                        older();
                    }

                    e.Handled = true;
                    break;
                case Keys.Right:
                    if (e.Control)
                    {
                        forward();
                    }
                    else
                    {
                        reOlder();
                    }

                    e.Handled = true;
                    break;
                case Keys.S:
                    if (e.Control)
                    {
                        FormMain.saveAll();
                    }
                    else
                    {
                        setOperate(Operate.select);
                    }

                    break;
                case Keys.R:
                    if (e.Control)
                    {
                        if (richTextBox1.Text.StartsWith("s:"))
                        {
                            goGlobal(richTextBox1.Text.Substring(2));
                        }
                        else
                        {
                            go(recordHistory: false, goSelect: selectCard != null);
                        }
                        System.Media.SystemSounds.Beep.Play();
                        e.Handled = true;
                    }

                    break;
                case Keys.Up:
                    //setColor(false);
                    targetScrollY = scrollY + 100;
                    //setColor(true);
                    setToDraw();
                    //flowLayoutPanel1.ScrollControlIntoView(flowLayoutPanel1.Controls[scrollY]);
                    break;
                case Keys.Down:
                    //setColor(false);
                    targetScrollY = scrollY - 100;
                    setToDraw();
                    //setColor(true);
                    //flowLayoutPanel1.ScrollControlIntoView(flowLayoutPanel1.Controls[selectIndex]);
                    break;
                case Keys.Escape:
                    hideViewersAndShowNormalControls();
                    label3.Text = consoleDigital = "";
                    richTextBox5.Text = "";
                    setOperate(Operate.go);
                    clearSelectedAndHandleUI();
                    setToDraw();
                    break;
                case Keys.Enter:
                    int goNumI;
                    int.TryParse(consoleDigital, out goNumI);
                    goNum(goNumI);
                    newOpen = false;

                    break;
                case Keys.H:
                    if (e.Control)
                    {
                        qbSetting.home = getNowPath();
                    }
                    else
                    {
                        go(qbSetting.home);
                    }

                    break;
                case Keys.D:
                    if (operate == Operate.go)
                    {
                        if (mode == Mode.noThumb)
                        {
                            mode = Mode.full;
                        }
                        else
                        {
                            mode = ((Mode)(int)mode + 1);
                        }

                        Text = "Mode:" + mode.ToString();
                        setToDraw();
                    }
                    else if (operate == Operate.delete)
                    {
                        button5_Click(sender, e);
                        setOperate(Operate.go);
                    }

                    break;
                case Keys.E:
                    setOperate(Operate.delete);
                    break;
                case Keys.P:
                    button10_Click(sender, e);
                    break;
                case Keys.F:
                    {
                        string path;
                        string folder;
                        int flowIndex = 1;
                        do
                        {
                            folder = "New Folder " + flowIndex++;
                            path = Path.Combine(getNowPath(), folder);
                        } while (Directory.Exists(path));

                        try
                        {
                            Directory.CreateDirectory(path);
                            System.Media.SystemSounds.Beep.Play();
                            selecteGoto = true;
                            selectName = folder;
                            richTextBox5.Text = "";
                            resetCache();
                            go();
                        }
                        catch
                        {
                            System.Media.SystemSounds.Hand.Play();
                            return;
                        }
                    }
                    break;
                case Keys.T:
                    {
                        string path;
                        string fileName;
                        int flowIndex = 1;
                        do
                        {
                            fileName = "New Text " + flowIndex++ + ".txt";
                            path = Path.Combine(getNowPath(), fileName);
                        } while (File.Exists(path));

                        try
                        {
                            File.CreateText(path).Close();
                            System.Media.SystemSounds.Beep.Play();
                            selecteGoto = true;
                            selectName = fileName;
                            richTextBox5.Text = "";
                            resetCache();
                            go();
                        }
                        catch
                        {
                            System.Media.SystemSounds.Hand.Play();
                            return;
                        }
                    }
                    break;
                case Keys.L:
                    Parallel.For(0, cardList.Count, (i) =>
                    {
                        toggleSelectedCard(cardList[i]);
                    });
                    Parallel.For(0, cardList.Count, (i) =>
                    {
                        if (cardList[i] != null)
                        {
                            cardList[i].index = i;
                            numToFileDctionaryCard[i] = cardList[i];
                        }
                    });
                    updateZ();
                    break;
            }
        }
        public enum Operate
        {
            go, delete, select
        }
        public Operate operate = Operate.go;
        public void setOperate(Operate operate)
        {
            this.operate = operate;
            switch (operate)
            {
                case Operate.go:
                    //richTextBox1.SelectionColor = Color.Black;
                    richTextBox2.BackColor = Color.White;
                    break;
                case Operate.delete:
                    //richTextBox1.SelectionColor = Color.White;
                    richTextBox2.BackColor = Color.Red;
                    break;
                case Operate.select:
                    //richTextBox1.SelectionColor = Color.Black;
                    richTextBox2.BackColor = Color.Yellow;
                    break;
                default:
                    break;
            }
        }

        public static FilePathCacheListManager filePathCacheListManager;
        public static QBSetting qbSetting;
        public static HashSet<Form1> closedForms = new HashSet<Form1>();

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (exit)
            {
                return;
            }

            history.Clear();
            histroyIndex = 0;

            _mediaPlayer.Stop();
            goButNotSame(qbSetting.home);
            closedForms.Add(this);
            e.Cancel = true;
            WindowState = FormWindowState.Normal;
            Hide();
            //Top = 5000;
            if (FormMain.lastForm == this)
            {
                FormMain.lastForm = null;
            }
        }

        public static Form1 getForm1(bool open = true)
        {
            if (closedForms.Count == 0)
            {
                return new Form1();
            }

            var e = closedForms.GetEnumerator();
            e.MoveNext();
            var form1 = e.Current;
            form1.hideViewersAndShowNormalControls();
            if (open)
            {
                form1.nowPath = "";
                form1.Show();
            }
            form1.Top = 0;
            closedForms.Remove(form1);
            return form1;
        }

        public void kill()
        {
            smartThreadPool.Dispose();
            smartThreadPool2.Dispose();
            smartThreadPool3.Dispose();

            drawing = false;
            exit = true;
            while (drawThread.IsAlive || scrollSmoothMove.IsAlive)
            {
                System.Windows.Forms.Application.DoEvents();
                Thread.Sleep(1);
            }
            drawThread.Abort();
            scrollSmoothMove.Abort();
            Close();
        }

        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            Text = "a";
        }

        private void Form1_Scroll(object sender, ScrollEventArgs e)
        {

        }

        private void flowLayoutPanel1_Scroll(object sender, ScrollEventArgs e)
        {
            //flowLayoutPanel1.Refresh();
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
        }
        void goNum(int i)
        {
            if (numToFileDctionaryCard.ContainsKey(i))
            {
                var target = numToFileDctionaryCard[i];
                if (target.onMouseDown == null) return;
                switch (operate)
                {
                    case Operate.go:
                        target.onMouseDown();
                        gotoIndex = -1;
                        break;
                    case Operate.delete:
                        clearSelectedAndHandleUI();
                        addSelectedCard(target);
                        button5_Click(null, EventArgs.Empty);
                        setOperate(Operate.go);
                        break;
                    case Operate.select:
                        selecteGoto = true;
                        clearSelectedAndHandleUI();
                        setGotoCard(target.index);
                        setOperate(Operate.go);
                        setToDraw();
                        break;
                    default:
                        break;
                }
                //Text = $"goNum:{i}";
                label3.Text = consoleDigital = "";
            }
            else
            {
                Text = $"goNum:{i} not found";
                System.Media.SystemSounds.Hand.Play();
                label3.Text = consoleDigital = "";
            }
            setToDraw();
        }
        void goNumOpen(int i)
        {
            if (numToFileDctionaryCard.ContainsKey(i))
            {
                if (numToFileDctionaryCard[i].onMouseMiddleDown != null)
                {
                    numToFileDctionaryCard[i].onMouseMiddleDown();
                }
                label3.Text = consoleDigital = "";
            }
            else
            {
                System.Media.SystemSounds.Hand.Play();
                label3.Text = consoleDigital = "";
            }
            setToDraw();
        }

        private void Form1_MouseDown(object sender, MouseEventArgs e)
        {

        }
        bool newOpen = false, search = false;
        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {
            if (richTextBox2.Text.Length < 1)
                return;
            char c = richTextBox2.Text[0];
            //Debug.WriteLine(c);
            //targetScrollY = scrollY = 0;

            int go;
            if (char.IsDigit(c))
            {
                consoleDigital += c;
                updateConsoleDigital();
                int.TryParse(consoleDigital, out go);
                //richTextBox2.Text = consoleDigital;
                if (consoleDigital.Length >= maxConsoleDigital.Length)
                {
                    if (newOpen)
                    {
                        goNumOpen(go);
                    }
                    else
                    {
                        goNum(go);
                    }
                    newOpen = false;
                    richTextBox2.BackColor = Color.White;
                    label3.Text = "";
                }
                setToDraw();
            }
            else if (c == '-')
            {
                if (consoleDigital.Length > 0)
                {
                    consoleDigital = consoleDigital.Remove(consoleDigital.Length - 1);
                }
                updateConsoleDigital();
                setToDraw();
            }
            else if (c == '+')
            {
                newOpen = !newOpen;
                if (newOpen)
                {
                    richTextBox2.BackColor = Color.Yellow;
                }
                else
                {
                    richTextBox2.BackColor = Color.White;
                }
            }
            else if (c == 'x')
            {
                Close();
            }
            if (c == '*')
            {
                search = !search;
                if (search)
                {
                    richTextBox5.Select();
                }
                else
                {
                    richTextBox2.Select();
                }
                updateConsoleDigital();
            }
            richTextBox2.Text = "";
        }
        void updateConsoleDigital()
        {
            label3.Text = consoleDigital;
        }
        private void button1_Click(object sender, EventArgs e)
        {
            richTextBox2.Select(0, 0);
            richTextBox2.Focus();
            back();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            richTextBox2.Select(0, 0);
            richTextBox2.Focus();
            older();
        }

        private void richTextBox2_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = true;
        }
        float oldY;
        PointF? p1, p2;
        bool leftScrollDown;
        private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            /*if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                scrollY += e.Y - oldY;
                oldY = e.Y;
                setToDraw();
            }*/

            //right scroll bar
            if (e.Button == MouseButtons.Left)
            {
                RectangleF rectangle = new RectangleF(blockRight - 20 - 10, 20, 20, blockRight - 40);
                if (rectangle.Contains(e.Location) || leftScrollDown)
                {
                    leftScrollDown = true;
                    targetScrollY = scrollY = -(float)(e.Location.Y - 20) / (bitmapHeight - 40) * getScrollHeight();
                    setToDraw();
                    return;
                }
                else
                {
                    leftScrollDown = false;
                }
            }
            else
            {
                leftScrollDown = false;
            }


            if (e.Button == MouseButtons.None)
            {
                return;
            }
            if (e.Button == MouseButtons.Right) p2 = e.Location;
            if (e.Button == MouseButtons.Right && p1.HasValue && p2.HasValue && (selectRectangle.Width > 5 || selectRectangle.Height > 5))
            {
                pictureBox2.Dock = DockStyle.None;
                pictureBox2.Anchor = AnchorStyles.None;
                pictureBox2.Left = pictureBox2.Width / 2;
                pictureBox2.Top = pictureBox2.Height / 2;
                //clearSelectedAndHandleUI();
                updateSelectRectangle();
                richTextBox4.Text = $"Selected Count: {selected.Count}";
                setToDraw();
            }
        }

        private void updateSelectRectangle()
        {
            var selectRectangleEx = selectRectangle;
            //selectRectangleEx.Y -= (bitmapHeight - pictureBox1.Height) / 2;
            for (int i = 0; i < cardList.Count; i++)
            {
                var card = cardList[i];
                if (card.bound.IntersectsWith(Rectangle.Round(selectRectangleEx)))
                {
                    addSelectedCard(card);
                }
                else
                {
                    removeSelectedCard(card);
                }

                /*if (card.bound.Y > selectRectangle.Bottom)
                {
                    break;
                }*/
            }

            for (int i = 0; i < ctrlRemeber.Count; i++)
            {
                var r = ctrlRemeber[i];
                if (!selected.Contains(r))
                {
                    selected.Add(r);
                }

                r.isSelected = true;
            }
        }

        private FileDirectoryCard selectCard;
        List<FileDirectoryCard> selected = new List<FileDirectoryCard>();
        void clearSelectedAndHandleUI()
        {
            lock (selected)
            {
                Parallel.ForEach(selected, (card) => card.isSelected = false);
                selected.Clear();
            }

            callMain(() =>
            {
                lockRichText3 = true;
                richTextBox3.Text = "";
                lockRichText3 = false;

                lockRichTextBox7 = false;
                richTextBox4.Text = "";
                label2.Text = "";
            });
        }
        void selectedHandleUI()
        {
            //label7.Visible = richTextBox3.Visible = selected.Count == 1;
            StringBuilder stringBuilder = new StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                var s = selected[i];
                stringBuilder.Append(s.fileName);
                stringBuilder.Append(",");
            }
            if (selected.Count > 0)
            {
                stringBuilder.Remove(stringBuilder.Length - 1, 1);
            }
            label2.Text = $"Total:{selected.Count}";
            if (selected.Count > 1)
            {
                richTextBox4.Text = stringBuilder.ToString();
            }
            setToDraw();
        }
        bool lockPictureBoxMouswDown = false;
        List<FileDirectoryCard> ctrlRemeber = new List<FileDirectoryCard>();
        private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (lockPictureBoxMouswDown) return;
            selectStart = scrollY;
            p1 = e.Location;
            p2 = null;
            ctrlRemeber.Clear();

            //Debug.WriteLine(e.Location.ToString());
            FileDirectoryCard oldSelected = selectCard;
            bool move = false, copy = false;
            if (Control.ModifierKeys != Keys.Control)
            {
                if (e.Button == MouseButtons.Right)
                {
                    if (Control.ModifierKeys == Keys.Shift)
                    {
                        move = true;
                    }
                    else if (Control.ModifierKeys == Keys.Control)
                    {
                        copy = true;
                    }
                    else
                    {
                        clearSelectedAndHandleUI();
                    }
                }
                else
                {
                    clearSelectedAndHandleUI();
                }
            }
            else
            {
                //ctrl
                if (e.Button == MouseButtons.Left)
                {
                    for (int i = 0; i < selected.Count; i++)
                    {
                        selected[i].onMouseDown?.Invoke();
                    }

                    return;
                }
            }

            selectCard = null;
            for (int i = 0; i < cardList.Count; i++)
            {
                FileDirectoryCard card = cardList[i];
                if (card.bound.Top > e.Location.Y)
                {
                    break;
                }
                Point point = e.Location;
                //point.Offset((pictureBox1.Width - bitmapWidth) / -2, (pictureBox1.Height - bitmapHeight) / -2);
                if (card != null && card.bound.Contains(point))
                {
                    selectCard = card;
                    //setToDraw();
                    if (card.onMouseDown != null)
                    {
                        if (e.Button == MouseButtons.Left)
                        {
                            saveNowState();
                            card?.onMouseDown?.Invoke();
                        }
                        else if (e.Button == MouseButtons.Middle)
                        {
                            card?.onMouseMiddleDown?.Invoke();
                        }
                        break;
                    }
                }
            }
            if (selectCard == null)
            {
                if (Control.ModifierKeys != Keys.Control)
                {
                    clearSelectedAndHandleUI();
                    //setToDraw();
                }
            }
            else
            {
                if (move)
                {
                    richTextBox6.Text = selectCard.fullPath;
                    button8_Click(sender, e);
                    return;
                }
                else if (copy)
                {
                    richTextBox6.Text = selectCard.fullPath;
                    button7_Click(sender, e);
                    return;
                }
                if (oldSelected == selectCard && e.Button != MouseButtons.Middle)
                {
                    removeSelectedCard(selectCard);
                    oldSelected = selectCard = null;
                }
                else
                {
                    toggleSelectedCard(selectCard);
                }
            }
            if (Control.ModifierKeys == Keys.Control)
            {
                if (e.Button == MouseButtons.Right)
                {
                    ctrlRemeber.Clear();
                    ctrlRemeber.AddRange(selected);
                    richTextBox4.Text = $"Selected Count: {selected.Count}";
                    return;
                }
            }
            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
                oldY = e.Y;
                if (Control.ModifierKeys != Keys.Control)
                {
                    setToDraw();
                }
                lockRichTextBox7 = true;
                if (selected.Count == 1 && qbSetting.fileToImage.ContainsKey(selectCard.fullPath))
                {
                    richTextBox7.Text = qbSetting.fileToImage[selectCard.fullPath];
                }
                else
                {
                    richTextBox7.Text = "";
                }
                lockRichTextBox7 = false;
                selectedHandleUI();
            }
            if (e.Button == MouseButtons.Right)
            {
                if (selectCard != null)
                {
                    lock (selectCard)
                    {

                        if (selectCard.type == FileDirectoryCard.Type.disk)
                        {
                            button5.Enabled = false;
                        }
                        else
                        {
                            button5.Enabled = true;
                        }
                        if (File.Exists(selectCard.fullPath))
                        {
                            FileInfo info = new FileInfo(selectCard.fullPath);
                            goodSizeShow(info.Length);
                        }
                        else if (Directory.Exists(selectCard.fullPath))
                        {
                            label2.Text = "Caculating...";
                            if (selectCard.fullPath.Length <= 3)
                            {
                                //disk
                                DriveInfo driveInfo = new DriveInfo(selectCard.fullPath);
                                goodSizeShow(driveInfo.TotalSize - driveInfo.TotalFreeSpace, true);
                                label2.Text += $"Total Size: {FileSizeFormatter.FormatSize(driveInfo.TotalSize)}\nUsed Percent: {((driveInfo.TotalSize - driveInfo.TotalFreeSpace) * 100.0 / driveInfo.TotalSize).ToString("0.00")}%";
                            }
                            else
                            {
                                //dir
                                label2.Text = "Caculating Directory";
                            }
                        }
                    }
                }
            }
            if (selected.Count == 0)
            {
                showBackgroundPathText(getNowPath());
            }
        }

        private void goodSizeShow(long dirSize, bool isDisk = false)
        {
            if (dirSize == -1)
            {
                label2.Text = "Error";
            }
            else
            {
                label2.Text = "";
                if (isDisk)
                {
                    label2.Text += "Used ";
                }
                if (FileEx.error)
                {
                    label2.Text += "Size: <" + FileSizeFormatter.FormatSize(dirSize) + "\n";
                }
                else
                {
                    label2.Text += "Size: " + FileSizeFormatter.FormatSize(dirSize) + "\n";
                }
            }
        }

        private void pictureBox1_Resize(object sender, EventArgs e)
        {
        }

        private void button4_Click(object sender, EventArgs e)
        {
        }

        bool lockRichText3 = false;
        private void richTextBox3_TextChanged(object sender, EventArgs e)
        {
            if (lockRichText3) return;
            if (selectCard != null)
            {
                string path = richTextBox3.Text.Replace("\r", "").Replace("\n", "");
                if (path == "") return;
                string d = Path.GetDirectoryName(selectCard.fullPath);
                if (d == null)
                    return;
                string newPath = Path.Combine(d, path);
                //Debug.WriteLine(newPath);
                if (selectCard.fullPath == newPath)
                {
                    return;
                }
                string r7 = richTextBox7.Text;
                addJob(() =>
                {
                    qbSetting.fileToImage.TryRemove(selectCard.fullPath, out string a);
                    try
                    {
                        if (File.Exists(selectCard.fullPath))
                        {
                            System.IO.File.SetAttributes(selectCard.fullPath, System.IO.FileAttributes.Normal);
                            File.Move(selectCard.fullPath, newPath);
                        }
                        else if (Directory.Exists(selectCard.fullPath))
                        {
                            Directory.Move(selectCard.fullPath, newPath);
                        }
                    }
                    catch (Exception ex2)
                    {
                        System.Media.SystemSounds.Hand.Play();
                        return;
                    }

                    if (filePathCacheListManager.imagePathToCacheJPGFile.TryRemove(selectCard.fullPath, out string b))
                    {
                        FileInfo f = new FileInfo(newPath);
                        filePathCacheListManager.imagePathToEditTime[newPath] = f.LastWriteTime;
                        filePathCacheListManager.imagePathToCacheJPGFile[newPath] = b;
                        filePathCacheListManager.JPGFileToimagePath[b] = newPath;
                    }
                    selectCard.fileName = Path.GetFileName(newPath);
                    selectCard.fullPath = newPath;
                    if (!string.IsNullOrEmpty(r7))
                    {
                        qbSetting.fileToImage[selectCard.fullPath] = r7;
                    }
                    addAndInitCard(selectCard.index, selectCard.fullPath);
                    resetCache();
                    setToDraw();
                    System.Media.SystemSounds.Beep.Play();
                    richTextBox4.Invoke(new Action(() =>
                    {
                        richTextBox4.Text = newPath + "\n\n" + selectCard.fullPath2;
                    }));

                });
            }

        }

        private void button5_Click(object sender, EventArgs e)
        {
            richTextBox4.Text = "";

            var targets = new List<FileDirectoryCard>();
            targets.AddRange(selected);

            addJobFree(() =>
            {
                bool allSuccess = true;
                Parallel.ForEach(targets, (card) =>
                {
                    string d = Path.GetDirectoryName(card.fullPath);
                    if (d == null)
                        return;
                    try
                    {
                        if (File.Exists(card.fullPath))
                        {
                            //File.Delete(card.fullPath);
                            FileSystem.DeleteFile(card.fullPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                        }
                        else if (Directory.Exists(card.fullPath))
                        {
                            //Directory.Delete(card.fullPath, true);
                            FileSystem.DeleteDirectory(directory: card.fullPath, showUI: UIOption.OnlyErrorDialogs,
                                recycle: RecycleOption.SendToRecycleBin,
                                UICancelOption.ThrowException);
                        }
                        if (filePathCacheListManager.imagePathToCacheJPGFile.TryRemove(card.fullPath, out var a))
                        {
                            filePathCacheListManager.JPGFileToimagePath.TryRemove(a, out _);
                        }
                        filePathCacheListManager.folderToCardList.Remove(card.fullPath);
                        filePathCacheListManager.folderToChildDirectoryList.Remove(card.fullPath);
                        filePathCacheListManager.folderToChildFileList.Remove(card.fullPath);
                        filePathCacheListManager.pathStateHashSet.TryRemove(card.fullPath, out _);
                        qbSetting.folderToBackgroundPath.TryRemove(card.fullPath, out _);
                        qbSetting.folderToImageShowType.TryRemove(card.fullPath, out _);
                        qbSetting.fileToImage.TryRemove(card.fullPath, out _);
                        setToDraw();
                    }
                    catch
                    {
                        System.Media.SystemSounds.Hand.Play();
                        allSuccess = false;
                    }
                });
                if (allSuccess)
                {
                    for (int i = 0; i < targets.Count; i++)
                    {
                        cardList.Remove(targets[i]);
                    }
                    selected.Clear();
                    System.Media.SystemSounds.Beep.Play();
                }
                else
                {
                    System.Media.SystemSounds.Hand.Play();
                }
                selectCard = null;
            });
        }

        int ii = 0;
        int imageCount = 0;
        private void richTextBox5_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && e.Control)
            {
                goNum(firstDrawIndex);
                richTextBox5.Text = "";
            }
            else if (e.KeyCode == Keys.Enter)
            {
                string filter = richTextBox5.Text;
                richTextBox5.Text = "";
                saveNowState();
                richTextBox5.Text = filter;
                goGlobal(richTextBox5.Text);
            }
        }
        bool addListToFZDZ(string p, string s, List<string> fz, List<string> dz)
        {
            bool someError = false;
            //
            string[] fz2, dz2;
            try
            {
                fz2 = Directory.GetFiles(p);
                dz2 = Directory.GetDirectories(p);
                for (int i = 0; i < fz2.Length; i++)
                {
                    string f = fz2[i];
                    //if (File.Exists(f))
                    {
                        string fz2in = Path.GetFileName(f);
                        if (Regex.IsMatch(fz2in, s, RegexOptions.IgnoreCase))
                        {
                            fz.Add(f);
                            addAndInitFileCard(ii, f);
                            ii++;
                            setToDraw();
                        }
                    }
                }
                for (int i = 0; i < dz2.Length; i++)
                {
                    string d = dz2[i];
                    if (Directory.Exists(d))
                    {
                        string dz2in = Path.GetFileName(d);
                        if (Regex.IsMatch(dz2in, s, RegexOptions.IgnoreCase))
                        {
                            dz.Add(d);
                            addAndInitDirectoryCard(ii, d);
                            ii++;
                            setToDraw();
                        }
                        addJob(() =>
                        {
                            try
                            {
                                someError = someError || addListToFZDZ(d, s, fz, dz);
                            }
                            catch
                            {
                                someError = true;
                            }
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                someError = true;
            }
            return someError;
        }

        private void richTextBox5_KeyPress(object sender, KeyPressEventArgs e)
        {
        }

        private void button6_Click(object sender, EventArgs e)
        {
            smartThreadPool2.Cancel(true);
            smartThreadPool.Cancel(true);

            if (richTextBox1.Text.StartsWith("s:"))
            {
                older();
                return;
            }
            richTextBox5.Text = "";
            global = false;
            targetScrollY = scrollY = startTargetScrollY;
            saveNowState();
            go(true);
        }
        bool lockSaveState = false;
        private bool lockRichText5;
        private void richTextBox5_TextChanged(object sender, EventArgs e)
        {
            if (lockRichText5) return;

            targetScrollY = scrollY = 0;
            filterWord = richTextBox5.Text;
            filterWordz = filterWord.Split(' ');
            setToDraw();

            if (lockGlobal)
            {
                goGlobal(filterWord);
            }
        }

        private void goGlobal(string search, uint index = 0)
        {
            if (string.IsNullOrEmpty(search))
            {
                return;
            }
            selectName = Path.GetFileName(nowPath);
            smartThreadPool.Cancel();
            addJob(() =>
            {
                string up = selectName;
                cardList.Clear();
                smartThreadPool2.Cancel();
                smartThreadPool3.Cancel();
                smartThreadPool2.WaitForIdle();
                smartThreadPool3.WaitForIdle();

                search = search.Replace("\r", "");
                histroyMode = false;
                setNewHistroy("s:" + search);
                initBrowswerView();
                oldRichText = nowPath;
                string[] fz;
                string[] dz;
                DateTime[] dzdt;
                DateTime[] fzdt;
                List<string> strings = new List<string>();

                IEnumerable<EverythingEntry> results;

                results = FormMain.everything
                .SearchFor($"file:{search}")
                .WithOffset(0)
                .GetFields(RequestFlags.FullPathAndFileName | RequestFlags.DateRecentlyChanged)
                .Execute();
                fz = results.Select(x => x.FullPath).ToArray();
                fzdt = results.Select(x => x.DateRecentlyChanged ?? DateTime.MinValue).ToArray();

                strings.Clear();
                results = FormMain.everything
                .SearchFor($"folder:{search}")
                .WithOffset(0)
                .GetFields(RequestFlags.FullPathAndFileName | RequestFlags.DateRecentlyChanged)
                .Execute();
                dz = results.Select(x => x.FullPath).ToArray();
                dzdt = results.Select(x => x.DateRecentlyChanged ?? DateTime.MinValue).ToArray();

                cardList.Clear();

                int size = dz.Length + fz.Length;
                for (int i = 0; i < dz.Length; i++)
                {
                    addAndInitDirectoryCard(i, dz[i], dzdt[i]);
                    if (i % 50 == 0)
                    {
                        setToDraw();
                    }
                }
                for (int i = 0; i < fz.Length; i++)
                {
                    addAndInitFileCard(dz.Length + i, fz[i], fzdt[i]);
                    if (i % 50 == 0)
                    {
                        setToDraw();
                    }
                }
                label1.Invoke(new Action(() =>
                {
                    max = dz.Length + fz.Length - 1;
                    label1.Text = maxConsoleDigital = max.ToString();
                    richTextBox1.Text = "s:" + search;
                }));

                selectName = up;

            });
        }

        string old6 = "";
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (targetPath != old6)
            {
                callMain(() =>
                {
                    richTextBox6.Text = targetPath;
                    old6 = targetPath;
                });
            }

            if (MouseButtons != MouseButtons.Left)
            {
                Point screenCoordinates = Cursor.Position;
                Point clientCoordinates = this.PointToClient(screenCoordinates);
                groupBox1.Visible = (clientCoordinates.Y > Height * 0.8f && clientCoordinates.Y < Height) && vlcControl1.Visible;
            }


            if (!vlcControl1.Visible || first) { }
            else if (MouseButtons == MouseButtons.Middle)
            {
                timer5.Stop();
                vlcControl1.Visible = false;
                _mediaPlayer.Stop();
                first = true;
                hideViewersAndShowNormalControls();
            }
        }

        private void button3_MouseDown(object sender, MouseEventArgs e)
        {
            string text = richTextBox1.Text.Replace("\n", "").Replace("\r", "");
            if (e.Button == MouseButtons.Right)
            {
                targetPath = text;
            }
            Clipboard.SetText(text);
            System.Media.SystemSounds.Beep.Play();
        }

        private void button4_MouseDown(object sender, MouseEventArgs e)
        {
            string text = richTextBox4.Text.Replace("\n", "").Replace("\r", "");
            if (text == "")
                return;

            if (e.Button == MouseButtons.Right)
            {
                targetPath = text;
            }
            Clipboard.SetText(text);
            System.Media.SystemSounds.Beep.Play();
        }
        private static void copyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", System.IO.SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", System.IO.SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }
        private static void moveFilesRecursively(string sourcePath, string targetPath)
        {
            Directory.Move(sourcePath, Path.Combine(targetPath, Path.GetFileName(sourcePath)));
        }
        void copyOrMove(string targetDirPath, string[] fileDirArray, bool copy, bool move)
        {
            bool success = Directory.Exists(targetDirPath);
            if (!success) return;
            for (int i = 0; i < fileDirArray.Length; i++)
            {
                string fileDir = fileDirArray[i];
                try
                {
                    if (File.Exists(fileDir))
                    {
                        string fileName = Path.GetFileName(fileDir);
                        string target = Path.Combine(targetDirPath, fileName);
                        string justName = Path.GetFileNameWithoutExtension(fileName);
                        string extension = Path.GetExtension(fileName);
                        int k = 2;
                        while (File.Exists(target))
                        {
                            target = Path.Combine(targetDirPath, string.Concat(justName, k, extension));
                            //Debug.WriteLine(target);
                            k++;
                        }
                        if (copy)
                        {
                            FileSystem.CopyFile(fileDir, target, UIOption.AllDialogs, UICancelOption.ThrowException);
                        }
                        else if (move)
                        {
                            FileSystem.MoveFile(fileDir, target, UIOption.AllDialogs, UICancelOption.ThrowException);
                        }
                    }
                    else if (Directory.Exists(fileDir))
                    {
                        if (copy)
                        {
                            FileSystem.CopyDirectory(fileDir, Path.Combine(targetDirPath, Path.GetFileName(fileDir)), UIOption.AllDialogs, UICancelOption.DoNothing);
                        }
                        else if (move)
                        {
                            FileSystem.MoveDirectory(fileDir, Path.Combine(targetDirPath, Path.GetFileName(fileDir)), UIOption.AllDialogs, UICancelOption.DoNothing);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Media.SystemSounds.Hand.Play();
                    MessageBox.Show(ex.ToString());
                    success = false;
                }
            }
            if (success)
            {
                Debug.WriteLine("complete!");
                System.Media.SystemSounds.Beep.Play();
                Invoke(new Action(() =>
                {
                    Text = "Copy Full Success";
                }));
            }
            else
            {
                System.Media.SystemSounds.Hand.Play();
                Invoke(new Action(() =>
                {
                    Text = "Copy fail something";
                }));
            }
            resetCache();
            go(true);
        }
        /// <summary>
        /// COPY
        /// </summary>
        private void button7_Click(object sender, EventArgs e)
        {
            string dir = richTextBox6.Text;

            var targets = new List<FileDirectoryCard>();
            targets.AddRange(selected);

            addJobFree(() =>
            {
                bool success = Directory.Exists(dir);
                Action end = () =>
                {
                    if (success)
                    {
                        System.Media.SystemSounds.Beep.Play();
                        Text = "Copy Full Success";
                        Debug.WriteLine("complete!");
                    }
                    else
                    {
                        Text = "Copy fail something";
                        System.Media.SystemSounds.Hand.Play();
                    }
                    float recordScrollY = scrollY;
                    goOrGlobalGo();
                    targetScrollY = scrollY = recordScrollY;
                };

                if (!success)
                {
                    Invoke(end);
                    return;
                }

                for (int i = 0; i < targets.Count; i++)
                {
                    var s = targets[i];
                    try
                    {
                        if (s.type == FileDirectoryCard.Type.file)
                        {
                            string target = Path.Combine(dir, s.fileName);
                            string justName = Path.GetFileNameWithoutExtension(s.fileName);
                            string extension = Path.GetExtension(s.fileName);
                            int k = 2;
                            while (File.Exists(target))
                            {
                                target = Path.Combine(dir, string.Concat(justName, k, extension));
                                //Debug.WriteLine(target);
                                k++;
                            }
                            FileSystem.CopyFile(s.fullPath, target, UIOption.AllDialogs, UICancelOption.ThrowException);
                        }
                        else if (s.type == FileDirectoryCard.Type.directory)
                        {
                            FileSystem.CopyDirectory(s.fullPath, Path.Combine(dir, Path.GetFileName(s.fullPath)), UIOption.AllDialogs, UICancelOption.DoNothing);
                        }
                    }
                    catch
                    {
                        success = false;
                    }
                }

                Invoke(end);
            });
        }
        volatile Bitmap background;
        private volatile string backgroundImagePath = "";

        void updateSeletedCardInfo()
        {
            if (selectCard == null)
            {
                if (selected.Count <= 0)
                {
                    return;
                }
                selectCard = selected[0];
            }
            lockRichText3 = true;
            richTextBox3.Text = selectCard.fileName.Replace("\r", "").Replace("\n", "");
            lockRichText3 = false;
            richTextBox4.Text = selectCard.fullPath + "\n\n" + selectCard.fullPath2;
        }

        void addSelectedCard(FileDirectoryCard card)
        {
            card.isSelected = true;
            if (!selected.Contains(card))
            {
                selected.Add(card);
            }
            if (selected.Count == 1)
            {
                Invoke(new Action(updateSeletedCardInfo));
            }
        }
        void removeSelectedCard(FileDirectoryCard card)
        {
            card.isSelected = false;
            selected.Remove(card);
        }

        void updateSelectedCard(FileDirectoryCard card)
        {
            if (card.isSelected)
            {
                addSelectedCard(card);
            }
            else
            {
                removeSelectedCard(card);
            }
        }

        void toggleSelectedCard(FileDirectoryCard card)
        {
            card.isSelected = !card.isSelected;
            updateSelectedCard(card);
        }

        void updateSelectedCardImage()
        {
            updateCardImage(selectCard);
        }
        void updateCardImage(FileDirectoryCard card)
        {
            string backgroundPath = richTextBox7.Text.Replace("\r", "").Replace("\n", "");
            if (File.Exists(backgroundPath))
            {
                if (qbSetting.fileToImage.ContainsKey(card.fullPath) && qbSetting.fileToImage[card.fullPath] == backgroundPath) return;
                card.isCustomizationImageShow = true;
                card.image = null;
                card.loadingDraw = false;
                card.loadImage = null;
                qbSetting.fileToImage[card.fullPath] = backgroundPath;
                addAndInitCard(card.index, card.fullPath);
                setToDraw();
            }
            else
            {
                card.isCustomizationImageShow = false;
                card.image = null;
                card.loadingDraw = false;
                card.loadImage = null;
                string aString;
                qbSetting.fileToImage.TryRemove(card.fullPath, out aString);
                imagePathToThumbnailCachePool.TryRemove(card.fullPath, out var bitmap);
                addAndInitCard(card.index, card.fullPath);
                setToDraw();
            }
        }

        private bool lockRichTextBox7;
        private void richTextBox7_TextChanged(object sender, EventArgs e)
        {
            if (lockRichTextBox7) return;

            if (selected.Count == 1)
            {
                updateSelectedCardImage();
            }
            else if (selected.Count == 0)
            {
                string nowPath = getNowPath();
                if (string.IsNullOrEmpty(richTextBox7.Text))
                {
                    string a;
                    qbSetting.folderToBackgroundPath.TryRemove(nowPath, out a);
                }
                else
                {
                    qbSetting.folderToBackgroundPath[nowPath] = richTextBox7.Text;
                }
                updateFolderBackground(nowPath);
            }
        }

        private void setGoodBitmapAndResetGraphic()
        {
            int newWidth = -1, newHeight = -1;
            if (background == null)
            {
                (newWidth, newHeight) = (pictureBox1.Width, pictureBox1.Height);
            }
            else
            {
                lock (background)
                {
                    if (comboBox1IndexThreadSafe == 1)
                    {
                        if (pictureBox1.Width / (float)pictureBox1.Height < background.Width / (float)background.Height)
                            (newWidth, newHeight) = ((int)(1f * pictureBox1.Width), (int)(1f * (float)pictureBox1.Width * (float)background.Height / (float)background.Width));
                        else
                            (newWidth, newHeight) = ((int)((float)1f * pictureBox1.Height / (float)background.Height * (float)background.Width), (int)(1f * pictureBox1.Height));
                    }
                    else
                    {
                        (newWidth, newHeight) = (pictureBox1.Width, pictureBox1.Height);
                    }
                }
            }

            if (newWidth == -1 || newHeight == -1) return;
            if (newWidth != bitmapWidth || newHeight != bitmapHeight)
            {
                System.Drawing.Imaging.PixelFormat pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
                //這邊導致畫面閃爍
                //b = new Bitmap(newWidth, newHeight, pixelFormat);
                bitmapWidth = newWidth;
                bitmapHeight = newHeight;
                //pictureBox1.Image = b;
                //g = Graphics.FromImage(b);
            }

        }

        private void button9_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                goButNotSame(qbSetting.home);
            }
            else if (e.Button == MouseButtons.Right)
            {
                qbSetting.home = getNowPath();
            }
        }

        public static void DeleteDirectory(string targetDir)
        {
            string[] files = Directory.GetFiles(targetDir, "*", System.IO.SearchOption.AllDirectories);
            foreach (string file in files)
                File.Delete(file);
            new Microsoft.VisualBasic.Devices.Computer().FileSystem.DeleteDirectory(targetDir, DeleteDirectoryOption.DeleteAllContents);
        }

        public static void MoveDirectory(string source, string dest)
        {
            FileSystem.CopyDirectory(source, dest, UIOption.AllDialogs, UICancelOption.ThrowException);
            DeleteDirectory(source);
        }
        private void button8_Click(object sender, EventArgs e)
        {
            richTextBox6.Text = richTextBox6.Text.Replace("\n", "");
            string dir = richTextBox6.Text;

            var targets = new List<FileDirectoryCard>();
            targets.AddRange(selected);

            addJobFree(() =>
            {
                bool success = Directory.Exists(dir);
                if (success)
                {
                    Parallel.ForEach(targets, s =>
                    {
                        try
                        {
                            if (s.type == FileDirectoryCard.Type.file)
                            {
                                string target = Path.Combine(dir, s.fileName);
                                string justName = Path.GetFileNameWithoutExtension(s.fileName);
                                string extension = Path.GetExtension(s.fileName);
                                int k = 2;
                                while (File.Exists(target))
                                {
                                    target = Path.Combine(dir, string.Concat(justName, k, extension));
                                    //Debug.WriteLine(target);
                                    k++;
                                }
                                File.Move(s.fullPath, target);
                                //FileSystem.MoveFile(s.fullPath, target, UIOption.AllDialogs, UICancelOption.ThrowException);
                            }
                            else if (s.type == FileDirectoryCard.Type.directory)
                            {
                                string dest = Path.Combine(dir, Path.GetFileName(s.fullPath));
                                //FileSystem.MoveDirectory(s.fullPath, dest, UIOption.AllDialogs, UICancelOption.DoNothing);
                                //MoveDirectory(s.fullPath, dest);
                                Directory.Move(s.fullPath, dest);
                                //FileSystem.MoveDirectory(s.fullPath, Path.Combine(targetPath, Path.GetFileName(s.fullPath)), UIOption.AllDialogs, UICancelOption.ThrowException);
                            }
                        }
                        catch (Exception ex)
                        {
                            success = false;
                        }
                    });
                }

                Action end = () =>
                {
                    if (success)
                    {
                        System.Media.SystemSounds.Beep.Play();
                        Text = "Move Full Success";
                        Debug.WriteLine("complete!");
                    }
                    else
                    {
                        Text = "Move fail something";
                        System.Media.SystemSounds.Hand.Play();
                    }

                    float recordScrollY = scrollY;
                    goOrGlobalGo();
                    targetScrollY = scrollY = recordScrollY;
                };
                Invoke(end);
            });
        }
        volatile int recoredFirstIndex;
        FormWindowState formWindowState;
        private int oldWidth, oldHeight;
        private void Form1_ResizeEnd(object sender, EventArgs e)
        {
            if (oldWidth == Width && oldHeight == Height) return;
            resizing = false;
            resetPictureBoxMain();
            oldWidth = Width;
            oldHeight = Height;
        }

        private void Form1_SizeChanged(object sender, EventArgs e)
        {
            if (Dock != DockStyle.Fill)
            {
                pictureBox1.Height = ClientRectangle.Height - HeightMinus;
                pictureBox1.Width = Width - pictureBoxWidthLess;
            }
            if (WindowState != formWindowState)
            {
                resetPictureBoxMain();

                bool aa = vlcControl1.Visible;
                bool r8 = richTextBox8.Visible;
                bool p2 = pictureBox2.Visible;
                bool picLoop = loopPicture.Visible;
                if (WindowState == FormWindowState.Maximized && drawPanel.Dock == DockStyle.Fill)
                {
                    pictureBox1.Width = Width;
                    pictureBox1.Height = Height;
                    foreach (Control control in Controls)
                    {
                        if (control == drawPanel) continue;
                        control.Visible = false;
                    }
                }
                else
                {
                    restoreControlVisible();
                }

                richTextBox8.Visible = r8;
                pictureBox2.Visible = p2;
                loopPicture.Visible = picLoop;
                vlcControl1.Visible = aa;
            }
            formWindowState = WindowState;

            if (pictureBox2.Visible)
            {
                resetPictureBox2();
            }

            if (loopPicture.Visible)
            {
                foreach (Control control in Controls)
                {
                    control.Visible = false;
                }

                loopPicture.Visible = true;
                loopPicture.set(cardList, 2);
            }
        }

        private void restoreControlVisible()
        {
            foreach (Control control in Controls)
            {
                if (controlVisibleRecord.TryGetValue(control, out var v))
                {
                    control.Visible = v;
                }
            }
        }

        private void recordControlsVisible()
        {
            foreach (Control control in Controls)
            {
                controlVisibleRecord[control] = control.Visible;
            }
        }

        private void pictureBox2_MouseDown(object sender, MouseEventArgs e)
        {
            timer3.Enabled = false;
            oldMousePosX = e.X;
            oldMousePosY = e.Y;
            if (e.Button == MouseButtons.Middle)
            {
                hideViewersAndShowNormalControls();
                setToDraw();
            }
        }

        private void hideViewersAndShowNormalControls()
        {
            if (WindowState == FormWindowState.Maximized && Height >= Screen.PrimaryScreen.Bounds.Height && Width >= Screen.PrimaryScreen.Bounds.Width)
            {
                foreach (Control c in Controls)
                {
                    c.Visible = false;
                }

                drawPanel.Visible = true;
            }
            else
            {
                foreach (Control c in Controls)
                {
                    if (c == pictureBox2) continue;
                    if (c == pictureBox1) continue;
                    c.Visible = true;
                }

                richTextBox8.Visible = false;
                pictureBox2.Visible = false;
                loopPicture.Visible = false;
                vlcControl1.Visible = false;
                //stopScroll = false;
            }
            setToDraw();
            //if (pictureBox2.Image != null) pictureBox2.Image.Dispose();
        }

        private void label2_Click(object sender, EventArgs e)
        {
            if (label2.Text != "Caculating Directory") return;
            if (Directory.Exists(selectCard.fullPath))
            {
                label2.Text = "Caculating...";
                if (selectCard.fullPath.Length > 3)
                {
                    smartThreadPool.QueueWorkItem(() =>
                    {
                        DirectoryInfo di = new DirectoryInfo(selectCard.fullPath);
                        long dirSize = FileEx.startGetDirectorySize(di, true);
                        Action a = () =>
                        {
                            goodSizeShow(dirSize);
                        };
                        label2.Invoke(a);
                    });
                }
            }
        }

        private void richTextBox8_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                try
                {
                    //richTextBox8.SaveFile(richTextBox8.Tag.ToString());
                    File.WriteAllText(richTextBox8.Tag.ToString(), richTextBox8.Text);
                    System.Media.SystemSounds.Beep.Play();
                }
                catch
                {
                    System.Media.SystemSounds.Hand.Play();
                }
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            comboBox1IndexThreadSafe = comboBox1.SelectedIndex;
            string path = getNowPath();
            qbSetting.folderToImageShowType[path] = comboBox1.SelectedIndex;
            setGoodBitmapAndResetGraphic();
            setToDraw();
        }

        private void richTextBox2_Enter(object sender, EventArgs e)
        {
            //InputLanguage.CurrentInputLanguage = InputLanguage.FromCulture(CultureInfo.GetCultureInfo("en-us"));
        }

        private void button11_Click(object sender, EventArgs e)
        {
            richTextBox2.Select(0, 0);
            richTextBox2.Focus();
            reOlder();
        }

        private void button12_Click(object sender, EventArgs e)
        {
            richTextBox2.Select(0, 0);
            richTextBox2.Focus();
            forward();
        }

        private void richTextBox8_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                richTextBox8.Visible = false;
            }
        }

        bool lockComboBox2 { set; get; }
        private volatile bool waitAllDrawOK;
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lockComboBox2) return;
            sortMode = comboBox2.SelectedIndex;
            //waitAllDrawOK = true;
            resetCache();
            saveNowState();
            addJobFree(() =>
                {
                    smartThreadPool.WaitForIdle();
                    smartThreadPool2.WaitForIdle();
                    go();
                });
        }

        private void button10_Click(object sender, EventArgs e)
        {
            string path = getNowPath();
            addJob(() =>
            {
                try
                {
                    if (path == computerPath)
                    {
                        Process.Start("::{20d04fe0-3aea-1069-a2d8-08002b30309d}");
                    }
                    else
                    {
                        Process.Start("explorer", path);
                    }
                    //System.Media.SystemSounds.Beep.Play();
                }
                catch
                {
                    System.Media.SystemSounds.Hand.Play();
                }
            });
        }
        int oldMousePosX, oldMousePosY;
        volatile float scale = 1;

        private void Form1_DragDrop(object sender, DragEventArgs e)
        {
            addJobFree(() =>
            {
                string[] fileArray = e.Data.GetData(DataFormats.FileDrop) as string[];
                //MessageBox.Show(string.Join("\n", fileArray));
                string nowPath = getNowPath();
                if (Control.ModifierKeys == Keys.Control)
                {
                    copyOrMove(nowPath, fileArray, true, false);
                }
                else
                {
                    copyOrMove(nowPath, fileArray, false, true);
                }

                Invoke(new Action(() => { label9.Text = ""; }));
            });
        }
        private void Form1_DragEnter(object sender, DragEventArgs e)
        {
            label9.Text = "";
            //timer2.Enabled = true;
        }

        private void Form1_QueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
        }

        private void Form1_DragLeave(object sender, EventArgs e)
        {
            //timer2.Enabled = false; 
            label9.Text = "";
        }

        private void Form1_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (ModifierKeys == Keys.Control)
                {
                    e.Effect = DragDropEffects.Copy;
                    label9.Text = "Copy";
                }
                else
                {
                    e.Effect = DragDropEffects.Move;
                    label9.Text = "Move";
                }
            }
            else
            {
                e.Effect = DragDropEffects.None;
                label9.Text = "";
            }
        }

        private void Form1_GiveFeedback(object sender, GiveFeedbackEventArgs e)
        {

        }

        private void button13_MouseDown(object sender, MouseEventArgs e)
        {

            string[] selectedPathArray = (from s in selected select s.fullPath).ToArray();
            DataObject data = new DataObject(DataFormats.FileDrop, selectedPathArray);
            if (Control.ModifierKeys == Keys.Control)
            {
                DoDragDrop(data, DragDropEffects.Copy);

                Thread.Sleep(100);
                System.Media.SystemSounds.Beep.Play();
                goOrGlobalGo();
                Invoke(new Action(() =>
                {
                    Text = "Copy Full Success";
                }));
            }
            else
            {
                DoDragDrop(data, DragDropEffects.Move);

                Thread.Sleep(100);
                System.Media.SystemSounds.Beep.Play();
                goOrGlobalGo();
                Invoke(new Action(() =>
                {
                    Text = "Move Full Success";
                }));
            }
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            scale = (float)trackBar1.Value / 100;
            //textToImage.Clear();
            setToDraw();
        }

        private void pictureBox1_MouseHover(object sender, EventArgs e)
        {
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            if (selectCard == null)
            {
                selectCard = cardList[0];
            }
            if (changeImageIndex(-1))
            {
                selectCard = cardList[0];
                //changeImageIndex(-1);
            }
        }

        private void button14_Click(object sender, EventArgs e)
        {
            foreach (Control control in Controls)
            {
                control.Visible = false;
            }

            stopScroll = true;
            loopPicture.Visible = false;
            if (selected.Count == 1)
            {
                loopPicture.offsetIndex = selected[0].index;
                loopPicture.set(cardList, 1);
                loopPicture.Show();
            }
            else if (cardList.Count > 0)
            {
                loopPicture.offsetIndex = cardList[0].index;
                loopPicture.set(cardList, 1);
                loopPicture.Show();
            }
        }

        private void button15_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < selected.Count; i++)
            {
                var card = selected[i];
                if (filePathCacheListManager.imagePathToCacheJPGFile.TryGetValue(card.fullPath, out var jpeg))
                {
                    //使用已有縮圖
                    card.image = getSmallCenter(card.isImage || card.isOther, card.fullPath, jpeg);
                }
            }
        }

        private void button16_Click(object sender, EventArgs e)
        {
            //Bandizip.exe bx -target:auto -o:g:\ "G:\新增資料夾\1 (1).zip" "G:\新增資料夾\1(2).zip"

            ChooseBox chooseBox = new ChooseBox();
            if (chooseBox.ShowDialog() != DialogResult.OK) return;

            string encode = chooseBox.encode;

            string target;
            if (string.IsNullOrEmpty(richTextBox6.Text))
            {
                target = getNowPath();
            }
            else
            {
                target = richTextBox6.Text;
            }

            StringBuilder cardz = new StringBuilder();
            for (int i = 0; i < selected.Count; i++)
            {
                var card = selected[i];
                cardz.Append($"\"{card.fullPath}\" ");
            }

            var p = Process.Start($"Bandizip.exe", $"bx -cp:{encode} -target:auto -o:\"{target}\" {cardz}");
        }

        private void button17_Click(object sender, EventArgs e)
        {
            SelectBox selectBox = new SelectBox();
            if (selectBox.ShowDialog() != DialogResult.OK) return;
            string command = selectBox.command;
            while (true)
            {
                int oldCount = command.Length;
                command = command.Replace("  ", " ");
                if (oldCount == command.Length)
                {
                    break;
                }
            }
            string[] s = command.Split(' ');
            switch (s[0])
            {
                case "s":
                    int from, to;
                    from = int.Parse(s[1]);
                    to = int.Parse(s[2]);
                    for (int i = 0; i < cardList.Count; i++)
                    {
                        var card = cardList[i];
                        bool addSelect = card.index <= to && card.index >= from;
                        if (addSelect) addSelectedCard(card);
                    }
                    setToDraw();
                    break;
            }
        }

        private void Form1_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                leftScrollDown = false;
            }
        }

        private void pictureBox1_MouseUp_1(object sender, MouseEventArgs e)
        {
            oldY = e.Y;
            p1 = p2 = null;
            setToDraw();
        }

        private void pictureBox2_MouseMove(object sender, MouseEventArgs e)
        {
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            Top = 0;
        }

        bool resizing = false;
        private void Form1_Resize(object sender, EventArgs e)
        {
            if (resizing)
            {
                setToDraw();
                return;
            }

            resizing = true;
            if (WindowState == FormWindowState.Maximized && pictureBox1.Dock == DockStyle.Fill)
            {
                padding = 5;
            }
            else
            {
                padding = 20;
                pictureBox1.Width = Width - pictureBoxWidthLess;
            }
            resetPictureBoxMain();
        }

        private void vlcControl1_MouseDown(object sender, MouseEventArgs e)
        {

        }

        // 1. 滑鼠按下時：暫停
        private void trackBar2_MouseDown(object sender, MouseEventArgs e)
        {
            // 取得 TrackBar 控制項
            var tb = trackBar2;

            if (_mediaPlayer.State == VLCState.Playing || reverse)
            {
                _mediaPlayer.Stop();
                _mediaPlayer.Play();
                _mediaPlayer.Time = 0;
            }

            // 計算滑桿可用寬度（扣掉左右邊界）
            int trackWidth = tb.ClientSize.Width - 20;

            // 將滑鼠點擊位置轉換成比例
            double ratio = (double)(e.X - 10) / trackWidth;

            // 計算對應的 Value
            int newValue = tb.Minimum + (int)(ratio * (tb.Maximum - tb.Minimum));

            // 設定 Value
            tb.Value = Math.Max(tb.Minimum, Math.Min(tb.Maximum, newValue));
        }

        // 2. 滑鼠移動時：更新時間軸
        private void trackBar2_Scroll(object sender, EventArgs e)
        {
            timer4_Tick(sender, null);
        }
        bool pause;
        // 3. 滑鼠放開時：繼續播放
        private void trackBar2_MouseUp(object sender, MouseEventArgs e)
        {
            // 檢查是否是左鍵放開
            if (e.Button == MouseButtons.Left)
            {
                if (vlcControl1.Visible && _mediaPlayer != null)
                {
                    if (!pause)
                    {
                        _mediaPlayer.Stop();
                        _mediaPlayer.Time = 0;
                        _mediaPlayer.Play();
                    }
                    progress = _mediaPlayer.Time = trackBar2.Value;
                }
            }
        }

        private void timer4_Tick(object sender, EventArgs e)
        {

        }

        private void Form1_MouseMove(object sender, MouseEventArgs e)
        {
        }

        private void button18_Click(object sender, EventArgs e)
        {
            _mediaPlayer.Stop();
            _mediaPlayer.Time = 0;
            _mediaPlayer.Play();
        }

        private void button19_Click(object sender, EventArgs e)
        {
            _mediaPlayer.Stop();
        }

        private void button20_Click(object sender, EventArgs e)
        {
            _mediaPlayer.SetPause(true);
            pause = true;
        }

        private void button21_Click(object sender, EventArgs e)
        {
            _mediaPlayer.SetPause(false);
            pause = false;
        }

        bool reverse;
        private void button22_Click(object sender, EventArgs e)
        {
            reverse = !reverse;
            timer5.Enabled = reverse;
            if (reverse)
            {
                progress = _mediaPlayer.Time;
                _mediaPlayer.Play();
                _mediaPlayer.SetPause(true);
            }
            else
            {
                _mediaPlayer.SetPause(true);
                progress = _mediaPlayer.Time;
                _mediaPlayer.Play();
            }
        }
        long progress = 0;
        bool first = true;
        private void timer5_Tick(object sender, EventArgs e)
        {
            if (MouseButtons == MouseButtons.Left) return;
            _mediaPlayer.SeekTo(TimeSpan.FromMilliseconds(progress));
            progress -= 100;
            _mediaPlayer.SetPause(false);
            _mediaPlayer.Play();
            _mediaPlayer.SetPause(true);
            groupBox1.Refresh();

            if (_mediaPlayer.Time >= 0 && _mediaPlayer.Time < _mediaPlayer.Media.Duration)
            {
                trackBar2.Value = (int)_mediaPlayer.Time;

            }
        }

        private void Form1_MouseDown_1(object sender, MouseEventArgs e)
        {
        }

        private void vlcControl1_MouseDown_1(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                vlcControl1.Visible = false;
                _mediaPlayer.Stop();
            }
        }

        private void groupBox1_VisibleChanged(object sender, EventArgs e)
        {

        }

        private void timer6_Tick(object sender, EventArgs e)
        {
        }

        private void vlcControl1_VisibleChanged(object sender, EventArgs e)
        {
            if (vlcControl1.Visible && _mediaPlayer.Media != null)
            {
                int newMax = (int)_mediaPlayer.Media.Duration;
                int newValue = (int)Math.Min(_mediaPlayer.Time, _mediaPlayer.Media.Duration);
                trackBar2.Maximum = newMax;
                trackBar2.Minimum = 0;
                int v = 0;
                if (newValue <= newMax && (_mediaPlayer.IsPlaying || (_mediaPlayer.State == VLCState.Paused && reverse)))
                {
                    if (reverse)
                    {
                        v = (int)progress;
                    }
                    else
                    {
                        v = (int)(Math.Min(_mediaPlayer.Time, _mediaPlayer.Media.Duration));
                    }
                    if (v > newMax)
                    {
                        v = newMax;
                    }
                    if (v < trackBar2.Minimum)
                    {
                        v = trackBar2.Minimum;
                    }
                    trackBar2.Value = v;
                }
                if (_mediaPlayer.State == VLCState.Ended || (reverse && progress <= 0))
                {
                    trackBar2.Value = newMax;
                    if (checkBox1.Checked)
                    {
                        if (reverse)
                        {
                            _mediaPlayer.Stop();
                            progress = _mediaPlayer.Time = _mediaPlayer.Media.Duration;
                            _mediaPlayer.Play();
                            trackBar2.Value = (int)_mediaPlayer.Media.Duration;
                        }
                        else
                        {
                            _mediaPlayer.Stop();
                            _mediaPlayer.Time = 0;
                            _mediaPlayer.Play();
                            trackBar2.Value = 0;
                        }
                    }
                }
            }
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            focusing = true;
        }

        private void Form1_Deactivate(object sender, EventArgs e)
        {
            focusing = false;
        }

        private void Form1_Enter(object sender, EventArgs e)
        {
            focusing = true;
        }

        private void Form1_Leave(object sender, EventArgs e)
        {
            focusing = false;
        }

        private void Form1_MouseEnter(object sender, EventArgs e)
        {
            focusing = true;
        }

        private void Form1_MouseLeave(object sender, EventArgs e)
        {
            focusing = false;
        }

        int bitmapWidth, bitmapHeight;

        private Dictionary<Control, bool> controlVisibleRecord = new Dictionary<Control, bool>();
        int workWidth;
        int workHeight;

        bool resetingGraphic;
        bool triggerNoSelectGoTo;
        private void resetPictureBoxMain()
        {
            //targetScrollY = scrollY = 0;
            if (bitmapWidth == pictureBox1.Width && bitmapHeight == pictureBox1.Height) return;
            if (pictureBox1.Width == 0 || pictureBox1.Height == 0) return;
            setGoodBitmapAndResetGraphic();
            if (!pictureBox2.Visible)
            {
                triggerNoSelectGoTo = true;
                selecteGoto = false;
                gotoIndex = recoredFirstIndex;
                setToDraw();
            }
            //targetScrollY = scrollY = y;

            drawPanel.Dock = pictureBox1.Dock;
            drawPanel.SetBounds(pictureBox1.Bounds.Left, pictureBox1.Bounds.Top, pictureBox1.Bounds.Width, pictureBox1.Bounds.Height);
            workWidth = pictureBox1.Bounds.Width;
            workHeight = pictureBox1.Bounds.Height;

            newGraphic();
        }
        void setGotoCard(int index)
        {
            gotoIndex = index;
            var card = cardList[gotoIndex];
            addSelectedCard(card);
            selectCard = card;
        }
        public static string targetPath = "";

        bool saveImage(Image image, string path)
        {
            //將Image轉換成流資料，並儲存為byte[]
            MemoryStream mstream = new MemoryStream();
            image.Save(mstream, System.Drawing.Imaging.ImageFormat.Png);
            byte[] byData = new Byte[mstream.Length];
            mstream.Position = 0;
            mstream.Read(byData, 0, byData.Length);
            mstream.Close();
            mstream.Dispose();
            File.WriteAllBytes(path, byData);
            return true;
        }
    }
}
