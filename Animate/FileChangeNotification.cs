using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace Animate
{
    internal class FileChangeNotification
    {
        private const uint FILE_NOTIFY_CHANGE_FILE_NAME = 0x00000001;
        private const uint FILE_NOTIFY_CHANGE_LAST_WRITE = 0x00000010;

        private const uint INFINITE = 0xFFFFFFFF;
        private const uint WAIT_OBJECT_0 = 0;
        private const uint WAIT_TIMEOUT = 0x00000102;

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindFirstChangeNotification(
            string lpPathName,
            bool bWatchSubtree,
            uint dwNotifyFilter);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FindNextChangeNotification(IntPtr hChangeHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool FindCloseChangeNotification(IntPtr hChangeHandle);

        [DllImport("kernel32.dll")]
        static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        internal string path;
        internal CancellationTokenSource cancel;

        public FileChangeNotification(string path, CancellationTokenSource cancel)
        {
            this.path = path;   
            this.cancel = cancel;
        }

        public void Wait()
        {
            var dirname = Path.GetDirectoryName(path);
            var filename = Path.GetFileName(path);

            if (dirname == null || filename == null)
                return;

            var lastChangeDate = File.GetLastWriteTime(path);

            IntPtr handle = FindFirstChangeNotification(dirname, false, FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE_LAST_WRITE);

            if (handle == IntPtr.Zero || handle == new IntPtr(-1))
            {
                Console.WriteLine("Erreur : " + Marshal.GetLastWin32Error());
                return;
            }

            Console.WriteLine($"Surveillance de : {path}");

            while (cancel.IsCancellationRequested == false)
            {
                uint result = WaitForSingleObject(handle, 5000);
                if (result == WAIT_OBJECT_0 || result == WAIT_TIMEOUT)
                {
                    var newDate = File.GetLastWriteTime(path);

                    if (newDate > lastChangeDate)
                    {
                        Application.Current.Dispatcher.Invoke(() => {
                            ((MainWindow)Application.Current.MainWindow).spriteChanged = true;
                        });
                        lastChangeDate = newDate;
                    }

                    // Reset pour continuer à écouter
                    if (!FindNextChangeNotification(handle))
                    {
                        Console.WriteLine("Erreur lors de FindNextChangeNotification");
                        break;
                    }
                }
            }

            FindCloseChangeNotification(handle);
        }
    }
}
