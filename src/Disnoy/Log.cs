using System;
using System.IO;

namespace Disnoy
{
    internal static class Log
    {
        private const long MaxSize = 2 * 1024 * 1024;
        private static readonly object Gate = new object();

        public static string Folder { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Disnoy");

        public static string FilePath { get; } = Path.Combine(Folder, "disnoy.log");

        public static void Write(string message)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine;
            lock (Gate)
            {
                try
                {
                    Directory.CreateDirectory(Folder);
                    RotateIfLarge();
                    File.AppendAllText(FilePath, line);
                }
                catch (IOException)
                {
                    // Logging must never take the app down.
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        private static void RotateIfLarge()
        {
            var file = new FileInfo(FilePath);
            if (!file.Exists || file.Length < MaxSize)
            {
                return;
            }
            string oldPath = Path.Combine(Folder, "disnoy.old.log");
            File.Delete(oldPath);
            File.Move(FilePath, oldPath);
        }
    }
}
