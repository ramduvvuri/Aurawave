using System;
using System.IO;
using System.Text;

namespace AuraWave.Utilities
{
    public static class AppLogger
    {
        private static readonly object SyncObj = new object();
        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AuraWave",
            "logs");
        private static readonly string LogPath = Path.Combine(LogDir, "aurawave.log");

        public static string LogFilePath => LogPath;

        public static void Info(string message) => WriteEntry("INFO", message, null);
        public static void Warn(string message) => WriteEntry("WARN", message, null);
        public static void Error(string message, Exception? ex = null) => WriteEntry("ERROR", message, ex);

        private static void WriteEntry(string level, string message, Exception? ex)
        {
            try
            {
                lock (SyncObj)
                {
                    if (!Directory.Exists(LogDir))
                        Directory.CreateDirectory(LogDir);

                    var sb = new StringBuilder();
                    sb.Append('[').Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'")).Append("] ");
                    sb.Append('[').Append(level).Append("] ").AppendLine(message);
                    if (ex != null)
                    {
                        sb.Append("  Exception: ").Append(ex.GetType().FullName).Append(": ").AppendLine(ex.Message);
                    }
                    File.AppendAllText(LogPath, sb.ToString(), Encoding.UTF8);
                }
            }
            catch
            {
                // Never crash the application if logging fails
            }
        }
    }
}