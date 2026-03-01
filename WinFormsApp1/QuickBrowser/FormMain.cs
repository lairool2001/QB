using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.MemoryMappedFiles;
using System.Threading;
using Amib.Threading;
using Amib.Threading.Internal;
using unvell.D2DLib.WinForm;
using Action = System.Action;
using System.Media;
using EverythingSharp.Fluent;

namespace QuickBrowser
{
    public partial class FormMain : Form
    {
        public static Form1 lastForm;
        public static Bitmap folder;
        public static EverythingSearcher everything;
        MemoryMappedViewAccessor reader;
        Thread listenOpen;
        public static SmartThreadPool smartThreadPool, smartThreadPool2, smartThreadPoolFree;
        public FormMain()
        {
            InitializeComponent();
            Hide();
            smartThreadPool = new SmartThreadPool();
            smartThreadPool2 = new SmartThreadPool();
            smartThreadPoolFree = new SmartThreadPool();

            /*notifyIcon1.Visible = true;
            notifyIcon1.ShowBalloonTip(1000, "", "QB is running", ToolTipIcon.Info);*/
        }
        public static void addJob(Action action)
        {
            WorkItemCallback workItemCallback = (obj) => { action(); return true; };
            smartThreadPool.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
        }
        public static void addJob2(Action action)
        {
            WorkItemCallback workItemCallback = (obj) => { action(); return true; };
            smartThreadPool2.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
        }
        public static void addJobFree(Action action)
        {
            WorkItemCallback workItemCallback = (obj) => { action(); return true; };
            smartThreadPoolFree.QueueWorkItem(workItemCallback, WorkItemPriority.Lowest);
        }

        public static void over(Action job1poolFinish)
        {
            addJob2(() =>
            {
                smartThreadPool.WaitForIdle();
                job1poolFinish?.Invoke();
            });
        }
        public static void saveAll()
        {
            saveFilePathCacheListManager();
            saveQBSetting();
            over(() =>
            {
                System.Media.SystemSounds.Beep.Play();
                MessageBox.Show("Save");
            });
        }

        const string constFileCacheFilePath = "Cache/FilePathCacheListManager.json";
        const string settingPath = "QBSetting.json";

        private void button1_Click(object sender, EventArgs e)
        {
            StartNewForm(Form1.qbSetting.home);
        }
        public Form1 StartNewForm(string path)
        {
            Form1 form1 = Form1.getForm1();
            Form1.SetForegroundWindow(form1.Handle.ToInt32());
            form1.WindowState = FormWindowState.Normal;
            form1.Top = 0;
            form1.Show();
            form1.goOrGlobalGo(path);
            form1.Focus();
            return form1;
        }

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        public static void saveQBSetting()
        {
            addJob(() =>
            {
                lock (Form1.qbSetting)
                {
                    File.WriteAllText(settingPath, JsonConvert.SerializeObject(Form1.qbSetting));
                }
            });
        }

        public static void saveFilePathCacheListManager()
        {
            addJob(() =>
            {
                lock (Form1.filePathCacheListManager)
                {
                    File.WriteAllText(constFileCacheFilePath,
                        JsonConvert.SerializeObject(Form1.filePathCacheListManager));
                }
            });
        }

        private bool exit;

        private void FormMain_Load(object sender, EventArgs e)
        {
            everything = new EverythingSearcher();

            folder = Properties.Resources.Image1;

            reader = Program.mmf.CreateViewAccessor();
            listenOpen = new Thread(new ThreadStart(() =>
            {
                while (!exit)
                {
                    Thread.Sleep(100);
                    int i = 0;
                    int a = reader.ReadInt32(i);
                    if (a == 1)
                    {
                        i += sizeof(int);
                        int size = reader.ReadInt32(i);
                        i += sizeof(int);
                        char[] chars = new char[size];
                        reader.ReadArray(i, chars, 0, chars.Length);
                        reader.Write(0, 0);
                        Invoke(new Action(async () =>
                        {
                            string goPath = new string(chars);
                            if (string.IsNullOrEmpty(goPath))
                            {
                                goPath = Form1.qbSetting.home;
                            }
                            if (lastForm == null)
                            {
                                lastForm = StartNewForm(goPath);
                            }
                            else
                            {
                                lastForm.goOrGlobalGo(goPath);
                            }
                            lastForm.WindowState = FormWindowState.Normal;
                            lastForm.Top = 0;
                            lastForm.Show();
                            lastForm.BringToFront();
                        }));
                    }
                }
            }));
            listenOpen.Priority = ThreadPriority.Lowest;
            listenOpen.Start();

            string dir = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\";
            Directory.SetCurrentDirectory(dir);
            FilePathCacheListManager filePathCacheListManager;
            if (!File.Exists(constFileCacheFilePath))
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                filePathCacheListManager = new FilePathCacheListManager();
                Directory.CreateDirectory(Path.GetDirectoryName(constFileCacheFilePath));
                File.WriteAllText(constFileCacheFilePath, JsonConvert.SerializeObject(filePathCacheListManager));
            }
            else
            {
                try
                {
                    filePathCacheListManager =
                        JsonConvert.DeserializeObject<FilePathCacheListManager>(
                            File.ReadAllText(constFileCacheFilePath));
                }
                catch
                {
                    filePathCacheListManager = null;
                }

                if (filePathCacheListManager.pathStateHashSet == null)
                {
                    filePathCacheListManager.pathStateHashSet = new ConcurrentDictionary<string, PathState>();
                }
                if (filePathCacheListManager.folderToCardList == null)
                {
                    filePathCacheListManager.folderToCardList = new Dictionary<string, List<FileDirectoryCard>>();
                }
            }
            Form1.filePathCacheListManager = filePathCacheListManager;

            QBSetting qbSetting;
            if (!File.Exists(settingPath))
            {
                qbSetting = new QBSetting();
                qbSetting.folderToBackgroundPath = new ConcurrentDictionary<string, string>();
                File.WriteAllText(settingPath, JsonConvert.SerializeObject(qbSetting));
            }
            else
            {
                try
                {
                    qbSetting = JsonConvert.DeserializeObject<QBSetting>(File.ReadAllText(settingPath));
                }
                catch
                {
                    qbSetting = new QBSetting();
                    qbSetting.folderToBackgroundPath = new ConcurrentDictionary<string, string>();
                    File.WriteAllText(dir + settingPath, JsonConvert.SerializeObject(qbSetting));
                }
            }

            if (qbSetting.fileToImage == null)
            {
                qbSetting.fileToImage = new ConcurrentDictionary<string, string>();
            }
            if (qbSetting.folderToImageShowType == null)
            {
                qbSetting.folderToImageShowType = new ConcurrentDictionary<string, int>();
            }
            if (qbSetting.folderToBackgroundPath == null)
            {
                qbSetting.folderToBackgroundPath = new ConcurrentDictionary<string, string>();
            }
            if (qbSetting.home == null)
            {
                qbSetting.home = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            }

            Form1.qbSetting = qbSetting;

            if (string.IsNullOrEmpty(Program.path))
            {
                Program.path = qbSetting.home;
            }
            StartNewForm(Program.path);
            Program.path = null;
        }

        private void toolStripMenuItem2_MouseDown(object sender, MouseEventArgs e)
        {

        }

        void close()
        {
            exit = true;

            var enumerator = Form1.aliveForms.GetEnumerator();
            while (enumerator.MoveNext())
            {
                enumerator.Current.kill();
            }
            Form1.smartThreadPoolFree.Dispose();
            Form1.aliveForms.Clear();
            Thread endThread = new Thread(new ThreadStart(() =>
            {
                saveFilePathCacheListManager();
                saveQBSetting();
                //smartThreadPool.Dispose();
                while (listenOpen.IsAlive)
                {
                    Application.DoEvents();
                    Thread.Sleep(1);
                }
                listenOpen.Abort();


                //smartThreadPool.WaitForIdle();
                smartThreadPool.Dispose();
                //smartThreadPool2.WaitForIdle();
                smartThreadPool2.Dispose();

                smartThreadPoolFree.WaitForIdle();
                smartThreadPoolFree.Dispose();

                Application.Exit();
            }));
            endThread.Start();

            Close();
        }
        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            close();
        }
        private void toolStripMenuItem1_MouseDown(object sender, MouseEventArgs e)
        {
            StartNewForm(Form1.qbSetting.home);
        }

        private void button2_Click(object sender, EventArgs e)
        {
        }

        private void FormMain_Shown(object sender, EventArgs e)
        {
            Hide();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            saveAll();
        }

        private void notifyIcon1_BalloonTipClicked(object sender, EventArgs e)
        {

        }

        private void notifyIcon1_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
                button1_Click(sender, e);
        }
    }
}
