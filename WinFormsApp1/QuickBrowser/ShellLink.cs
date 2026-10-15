using System;

namespace QuickBrowser
{
    /// <summary>
    /// 讀取 .lnk 捷徑。原本用 COM 參考（IWshRuntimeLibrary），.NET 8 的 dotnet CLI 無法建置 COM 參考，
    /// 改成執行時以 ProgID 晚期繫結 WScript.Shell，功能相同。
    /// </summary>
    internal static class ShellLink
    {
        static readonly Type ShellType = Type.GetTypeFromProgID("WScript.Shell");

        /// <summary>回傳 IWshShortcut（dynamic），可讀 TargetPath / WorkingDirectory / IconLocation。</summary>
        public static dynamic Open(string lnkPath)
        {
            dynamic shell = Activator.CreateInstance(ShellType);
            return shell.CreateShortcut(lnkPath);
        }
    }
}
