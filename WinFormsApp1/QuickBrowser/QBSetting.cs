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
        /// <summary>鎖定的背景（圖片、影片路徑或網址）；有值時所有資料夾都用這個背景。</summary>
        public string lockedBackground;
        /// <summary>鎖定的背景縮放方式（comboBox1：0 Cover、1 Zoom、2 Center）；有值時所有資料夾都用這個。</summary>
        public int? lockedImageShowType;
        public int volume;
    }
}