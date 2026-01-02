using IWshRuntimeLibrary;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Runtime.InteropServices;
using TsudaKageyu;

namespace QuickBrowser
{
    public class IconFunction
    {
        [ComImport()]
        [Guid("46EB5926-582E-4017-9FDF-E8998DAA0950")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IImageList
        {
            [PreserveSig]
            int Add(
                IntPtr hbmImage,
                IntPtr hbmMask,
                ref int pi);

            [PreserveSig]
            int ReplaceIcon(
                int i,
                IntPtr hicon,
                ref int pi);

            [PreserveSig]
            int SetOverlayImage(
                int iImage,
                int iOverlay);

            [PreserveSig]
            int Replace(
                int i,
                IntPtr hbmImage,
                IntPtr hbmMask);

            [PreserveSig]
            int AddMasked(
                IntPtr hbmImage,
                int crMask,
                ref int pi);

            [PreserveSig]
            int Draw(
                ref IMAGELISTDRAWPARAMS pimldp);

            [PreserveSig]
            int Remove(
                int i);

            [PreserveSig]
            int GetIcon(
                int i,
                int flags,
                ref IntPtr picon);
        };
        struct IMAGELISTDRAWPARAMS
        {
            public int cbSize;
            public IntPtr himl;
            public int i;
            public IntPtr hdcDst;
            public int x;
            public int y;
            public int cx;
            public int cy;
            public int xBitmap;
            public int yBitmap;
            public int rgbBk;
            public int rgbFg;
            public int fStyle;
            public int dwRop;
            public int fState;
            public int Frame;
            public int crEffect;
        }
        struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 254)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szTypeName;
        }

        const int SHGFI_SMALLICON = 0x1;
        const int SHGFI_LARGEICON = 0x0;
        const int SHIL_JUMBO = 0x4;
        const int SHIL_EXTRALARGE = 0x2;
        const int WM_CLOSE = 0x0010;

        public enum IconSizeEnum
        {
            SmallIcon16 = SHGFI_SMALLICON,
            MediumIcon32 = SHGFI_LARGEICON,
            LargeIcon48 = SHIL_EXTRALARGE,
            ExtraLargeIcon = SHIL_JUMBO
        }

        public static readonly IntPtr white = new IntPtr(0x0000000008b512a5);
        public static System.Drawing.Bitmap GetFileImageFromPath(
       string filepath, IconSizeEnum iconsize, bool isFile)
        {
            IntPtr hIcon = IntPtr.Zero;
            hIcon = GetIconHandleFromFilePath(filepath, iconsize);
            if (hIcon != IntPtr.Zero)
                return GetBitmapFromIconHandle(hIcon);
            else
                return null;
        }

        private static IntPtr GetIconHandleFromFilePath(string filepath, IconSizeEnum iconsize)
        {
            var shinfo = new SHFILEINFO();
            const uint SHGFI_SYSICONINDEX = 0x4000;
            const int FILE_ATTRIBUTE_NORMAL = 0x80;
            uint flags = SHGFI_SYSICONINDEX;
            return GetIconHandleFromFilePathWithFlags(filepath, iconsize, ref shinfo, FILE_ATTRIBUTE_NORMAL, flags);
        }

        private static IntPtr GetIconHandleFromFolderPath(string folderpath, IconSizeEnum iconsize)
        {
            var shinfo = new SHFILEINFO();

            const uint SHGFI_ICON = 0x000000100;
            const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
            const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            return GetIconHandleFromFilePathWithFlags(folderpath, iconsize, ref shinfo, FILE_ATTRIBUTE_DIRECTORY, flags);
        }

        private static System.Drawing.Bitmap GetBitmapFromIconHandle(IntPtr hIcon)
        {
            if (hIcon == IntPtr.Zero) return null;
            var myIcon = System.Drawing.Icon.FromHandle(hIcon);
            var bitmap = myIcon.ToBitmap();
            myIcon.Dispose();
            DestroyIcon(hIcon);
            SendMessage(hIcon, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            return bitmap;
        }

        private static IntPtr GetIconHandleFromFilePathWithFlags(
            string filepath, IconSizeEnum iconsize,
            ref SHFILEINFO shinfo, int fileAttributeFlag, uint flags)
        {
            const int ILD_TRANSPARENT = 1;
            var retval = SHGetFileInfo(filepath, fileAttributeFlag, ref shinfo, Marshal.SizeOf(shinfo), flags);
            if (retval == 0)
            {
                //throw (new System.IO.FileNotFoundException());
                return IntPtr.Zero;
            }
            var iconIndex = shinfo.iIcon;
            var iImageListGuid = new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");
            IImageList iml;
            var hres = SHGetImageList((int)iconsize, ref iImageListGuid, out iml);
            var hIcon = IntPtr.Zero;
            if (iml != null)
                hres = iml.GetIcon(iconIndex, ILD_TRANSPARENT, ref hIcon);
            return hIcon;
        }

        [DllImport("user32")]
        private static extern
            IntPtr SendMessage(
                IntPtr handle,
                int Msg,
                IntPtr wParam,
                IntPtr lParam);

        [DllImport("shell32.dll")]
        private static extern int SHGetImageList(
            int iImageList,
            ref Guid riid,
            out IImageList ppv);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetFileInfo(
            string pszPath,
            int dwFileAttributes,
            ref SHFILEINFO psfi,
            int cbFileInfo,
            uint uFlags);

        [DllImport("user32")]
        private static extern int DestroyIcon(
            IntPtr hIcon);
    }

    public static class FileIconHelper
    {
        /// <summary>
        /// 依副檔名取得高解析度的 Icon (轉成 Bitmap)
        /// </summary>
        public static Bitmap GetExtensionIcon(string extension, int iconIndex = 0)
        {
            // Step 1: 找到副檔名關聯的程式
            string progId = Registry.GetValue(@"HKEY_CLASSES_ROOT\" + extension, "", null) as string;
            if (progId == null) throw new Exception("沒有找到關聯程式: " + extension);

            string command = Registry.GetValue(@"HKEY_CLASSES_ROOT\" + progId + @"\shell\open\command", "", null) as string;
            if (command == null) throw new Exception("沒有找到開啟命令: " + progId);

            string exePath = command.Trim('"').Split(' ')[0];

            // Step 2: 用 IconExtractor 抽取 icon
            var extractor = new IconExtractor(exePath);

            // Step 3: 取得指定 index 的所有尺寸 icon
            Icon[] icons = extractor.GetAllIcons();

            // Step 4: 回傳最大尺寸的 Bitmap
            return icons[0].ToBitmap(); // ^1 表示最後一個 (通常是 256x256)
        }
        public static Icon GetLnkIcon(string lnkPath)
        {
            var shell = new WshShell();
            var shortcut = (IWshShortcut)shell.CreateShortcut(lnkPath);

            // 先看捷徑有沒有自訂 icon
            string iconLocation = shortcut.IconLocation;
            string targetPath = shortcut.TargetPath;

            string exePath = !string.IsNullOrEmpty(iconLocation) ? iconLocation.Split(',')[0] : targetPath;

            if (string.IsNullOrEmpty(exePath))
                throw new Exception("捷徑沒有目標路徑");

            // 用 IconExtractor 抓 icon
            var extractor = new TsudaKageyu.IconExtractor(exePath);
            return extractor.GetIcon(0);
        }
    }

}
