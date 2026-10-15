using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;

namespace QuickBrowser
{
    public class QBSetting
    {
        public ConcurrentDictionary<string, string> folderToBackgroundPath;
        public ConcurrentDictionary<string, int> folderToImageShowType;
        public ConcurrentDictionary<string, string> fileToImage;
        public string home;
        /// <summary>檔案卡片大小百分比（10~100），0 視為 100。</summary>
        public int cardScale;
        public int volume;
    }
}