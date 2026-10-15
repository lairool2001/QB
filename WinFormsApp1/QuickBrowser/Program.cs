using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace QuickBrowser
{
    public static class Program
    {
        static MemoryMappedViewAccessor writer;
        public static MemoryMappedFile mmf = MemoryMappedFile.CreateOrOpen("QuickBrowserShared", 1024);
        static Mutex mutex = new Mutex(true, "QB");
        public static string path = "";
        /// <summary>
        /// 應用程式的主要進入點。
        /// </summary>
        [STAThread]
        static void Main()
        {
            string[] args = Environment.GetCommandLineArgs();
            if (args.Length >= 2)
            {
                path = args[1];
            }
            else
            {
                path = "";
            }
            if (mutex.WaitOne(TimeSpan.Zero))
            {

            }
            else
            {
                writer = mmf.CreateViewAccessor();
                int i = 0;
                writer.Write(i, 1);
                i += sizeof(int);
                char[] chars = path.ToCharArray();
                writer.Write(i, chars.Length);
                i += sizeof(int);
                writer.WriteArray<char>(i, chars, 0, chars.Length);
                //MessageBox.Show("Same Program has started.");
                return;
            }
#if NETCOREAPP
            // .NET 8 的 WinForms 預設會感知 DPI、預設字型改成 Segoe UI；
            // 版面與自繪介面都以 .NET Framework 的行為（不感知 DPI、Microsoft Sans Serif 8.25）設計，維持一致
            Application.SetHighDpiMode(HighDpiMode.DpiUnaware);
            Application.SetDefaultFont(new System.Drawing.Font("Microsoft Sans Serif", 8.25f));
#endif
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var f = new FormMain();
            Application.Run(f);
            mutex.ReleaseMutex();
        }
    }
}
