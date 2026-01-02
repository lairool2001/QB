using System.Threading;

namespace QuickBrowser
{
    public static class FileEx
    {
        public static bool error = false;
        public static long startGetDirectorySize(this System.IO.DirectoryInfo directoryInfo, bool recursive = true)
        {
            error = false;
            return GetDirectorySize(directoryInfo, recursive, true);
        }
        private static long GetDirectorySize(this System.IO.DirectoryInfo directoryInfo, bool recursive = true, bool first = false)
        {
            var startDirectorySize = default(long);
            try
            {
                if (directoryInfo == null || !directoryInfo.Exists)
                    return startDirectorySize; //Return 0 while Directory does not exist.

                //Add size of files in the Current Directory to main size.
                foreach (var fileInfo in directoryInfo.GetFiles())
                {
                    try
                    {
                        System.Threading.Interlocked.Add(ref startDirectorySize, fileInfo.Length);
                    }
                    catch
                    {
                        error = true;
                    }
                }

                if (recursive) //Loop on Sub Direcotries in the Current Directory and Calculate it's files size.
                {
                    var p = System.Threading.Tasks.Parallel.ForEach(directoryInfo.GetDirectories(), (subDirectory) =>
                        {
                            try
                            {
                                System.Threading.Interlocked.Add(ref startDirectorySize, GetDirectorySize(subDirectory, recursive));
                            }
                            catch
                            {
                                error = true;
                            }
                        }
                    );
                    if (first)
                    {
                        while (!p.IsCompleted)
                        {
                            Thread.Sleep(1);
                        }
                    }
                }

            }
            catch
            {
                startDirectorySize = -1;
            }
            return startDirectorySize;  //Return full Size of this Directory.
        }
    }
}