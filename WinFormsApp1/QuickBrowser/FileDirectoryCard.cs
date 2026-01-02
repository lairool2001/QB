using System;
using System.Drawing;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using unvell.D2DLib;

namespace QuickBrowser
{
    public class FileDirectoryCard
    {
        public FileDirectoryCard clone()
        {
            FileDirectoryCard c = new FileDirectoryCard();
            c.index = index;
            c.fullPath = fileName;
            c.type = type;
            c.loadingDraw = false;
            return c;
        }

        private delegate void BitmapSetter(FileDirectoryCard card, Bitmap bitmap, PictureBox panel);

        private BitmapSetter bitmapSetter = null;
        private IAsyncResult result;
        public void setCardImageSafe(Bitmap bitmap, PictureBox panel)
        {
            if (bitmapSetter == null)
            {
                bitmapSetter = _setCardImageSafe;
            }
            loadingDraw = true;
            image = bitmap;
        }

        void _setCardImageSafe(FileDirectoryCard card, Bitmap bitmap, PictureBox panel)
        {
            try
            {
                if (bitmap == null)
                {
                    panel.EndInvoke(result);
                    return;
                }
                card.image = bitmap;
                panel.EndInvoke(result);
            }
            catch (Exception ex)
            {
                System.Media.SystemSounds.Hand.Play();
            }
        }

        [JsonIgnore]
        private DriveInfo driveInfo;
        [JsonIgnore]
        public long size
        {
            get
            {
                switch (type)
                {
                    case Type.file:
                        return (fileSystemInfo as FileInfo).Length;
                    case Type.directory:
                        return 0;
                    case Type.disk:
                        if (driveInfo == null)
                        {
                            driveInfo = new DriveInfo(fullPath);
                        }
                        return driveInfo.TotalSize - driveInfo.TotalFreeSpace;
                }
                return 0;
            }
        }

        public long getFullDiskSize
        {
            get
            {
                if (driveInfo == null)
                {
                    driveInfo = new DriveInfo(fullPath);
                }

                if (!driveInfo.IsReady) return -1;
                return driveInfo.TotalSize;
            }
        }
        private FileSystemInfo _fileSystemInfo = null;
        [JsonIgnore]
        public FileSystemInfo fileSystemInfo
        {
            get
            {
                if (_fileSystemInfo == null)
                {
                    if (type == Type.file)
                    {
                        FileInfo fileInfo = new FileInfo(fullPath);
                        _fileSystemInfo = fileInfo;
                    }
                    else if (type == Type.directory)
                    {
                        DirectoryInfo directoryInfo = new DirectoryInfo(fullPath);
                        _fileSystemInfo = directoryInfo;
                    }
                    else if (type == Type.disk)
                    {
                        return null;
                    }
                }
                else
                {
                    if (type == Type.file)
                    {
                        return _fileSystemInfo;
                    }
                    else if (type == Type.directory)
                    {
                        return _fileSystemInfo;
                    }
                    else if (type == Type.disk)
                    {
                        return null;
                    }
                }
                return _fileSystemInfo;
            }
        }

        public bool isShow { get; set; }

        public enum Type
        {
            file, directory, disk
        }
        public Type type;
        public int index = 0;
        volatile public string fullPath, fileName, fullPath2;

        public float imageWidth, imageHeight;
        [JsonIgnore] public Bitmap _image;
        [JsonIgnore]
        public Bitmap image
        {
            get
            {
                return _image;
            }
            set
            {
                imageWidth = value.Width;
                imageHeight = value.Height;
                _image = value;
                if (_image != null)
                {
                    _2image = _image.Clone() as Bitmap;
                }
            }
        }
        [JsonIgnore] public Bitmap _2image;
        [JsonIgnore]
        public Bitmap image2 => _2image;
        [JsonIgnore]
        public Action onMouseDown, onMouseMiddleDown;
        [JsonIgnore]
        internal Rectangle bound;
        [JsonIgnore]
        internal bool isImage, isVideo, isSelected, isOther, isIcon;
        [JsonIgnore]
        public Action loadImage;
        [JsonIgnore]
        public bool errorImage;
        [JsonIgnore]
        volatile internal bool loadingDraw;
        [JsonIgnore]
        public bool isCustomizationImageShow;

        public DateTime changedTime;

        public bool needDoImageThings(Mode mode)
        {
            switch (mode)
            {
                case Mode.full:
                    return true;
                case Mode.noIcon:
                    return !isOther;
                case Mode.noThumb:
                    return !isImage && !isVideo;
            }
            return false;
        }
    }
}