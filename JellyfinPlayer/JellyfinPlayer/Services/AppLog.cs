using System;
using System.Diagnostics;
using System.IO;
using System.IO.IsolatedStorage;

namespace JellyfinPlayer.Services
{
    // The app's only log: a text file in isolated storage, so a failure on the
    // phone can be read back without a cable or a debugger. Every write is best
    // effort - logging must never be the reason something fails.
    public static class AppLog
    {
        private const string Folder = "logs";
        private const string FileName = "app.log";
        // Beyond this the file keeps only its newest half.
        private const long MaxBytes = 128 * 1024;
        private static readonly object Sync = new object();

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message + (ex == null ? "" : Environment.NewLine + ex));
        }

        // Everything written so far, newest last. Empty when nothing was logged.
        public static string Read()
        {
            lock (Sync)
            {
                try
                {
                    var files = Storage();
                    string path = Path();
                    if (!files.FileExists(path)) return "";
                    using (var stream = files.OpenFile(path, FileMode.Open, FileAccess.Read))
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            return reader.ReadToEnd();
                        }
                    }
                }
                catch
                {
                    return "";
                }
            }
        }

        private static void Write(string level, string message)
        {
            string line = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") + " " + level + " " + message;
            Debug.WriteLine("[JellyfinPlayer] " + line);
            lock (Sync)
            {
                try
                {
                    var files = Storage();
                    if (!files.DirectoryExists(Folder)) files.CreateDirectory(Folder);
                    Trim(files);
                    using (var stream = files.OpenFile(Path(), FileMode.Append, FileAccess.Write))
                    {
                        using (var writer = new StreamWriter(stream))
                        {
                            writer.WriteLine(line);
                        }
                    }
                }
                catch
                {
                    // Storage full or locked: the message still reached the debugger.
                }
            }
        }

        // A phone should not grow a log without end; half the file is enough to
        // show how a session went wrong.
        private static void Trim(IsolatedStorageFile files)
        {
            string path = Path();
            if (!files.FileExists(path)) return;
            string text;
            using (var stream = files.OpenFile(path, FileMode.Open, FileAccess.Read))
            {
                if (stream.Length <= MaxBytes) return;
                using (var reader = new StreamReader(stream))
                {
                    text = reader.ReadToEnd();
                }
            }
            if (text.Length > MaxBytes / 2)
            {
                text = text.Substring(text.Length - (int)(MaxBytes / 2));
                int newline = text.IndexOf('\n');
                if (newline >= 0 && newline + 1 < text.Length) text = text.Substring(newline + 1);
            }
            using (var stream = files.CreateFile(path))
            {
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(text);
                }
            }
        }

        private static string Path()
        {
            return Folder + "\\" + FileName;
        }

        private static IsolatedStorageFile Storage()
        {
            return IsolatedStorageFile.GetUserStoreForApplication();
        }
    }
}
