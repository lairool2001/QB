using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace QuickBrowser
{
    public class FilePathCacheListManager
    {
        //List<FilePathCache> filePathCaches = new List<FilePathCache>();
        public Dictionary<string, List<FileDirectoryCard>> folderToCardList = new Dictionary<string, List<FileDirectoryCard>>();
        public Dictionary<string, string[]> folderToChildDirectoryList = new Dictionary<string, string[]>();
        public Dictionary<string, string[]> folderToChildFileList = new Dictionary<string, string[]>();
        public Dictionary<string, string[]> folderToChildFileAndDirectoryList = new Dictionary<string, string[]>();
        public volatile int imageFlow = 0;
        public const int imageFlowMax = 50000;
        public ConcurrentDictionary<string, DateTime> imagePathToEditTime = new ConcurrentDictionary<string, DateTime>();
        public ConcurrentDictionary<string, string> imagePathToCacheJPGFile = new ConcurrentDictionary<string, string>();
        public ConcurrentDictionary<string, string> JPGFileToimagePath = new ConcurrentDictionary<string, string>();
        public ConcurrentDictionary<string, PathState> pathStateHashSet = new ConcurrentDictionary<string, PathState>();
    }
}