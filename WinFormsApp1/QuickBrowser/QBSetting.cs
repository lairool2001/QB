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
    }
}