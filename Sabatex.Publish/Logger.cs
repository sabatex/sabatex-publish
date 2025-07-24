using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sabatex_publish
{
    internal static class Logger
    {
        private static object _sync = new object();
        public static string FilePath { get; private set; }
        public static LogLevel MinimumLevel { get; private set; }

        public static void Initialize(string filePath, LogLevel minLevel)
        {
            FilePath = filePath;
            MinimumLevel = minLevel;
            if (!string.IsNullOrEmpty(FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"=== Log started: {DateTime.Now} ==={Environment.NewLine}");
            }
        }

        public static void Log(LogLevel level, string message)
        {
            if (level < MinimumLevel) return;

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            lock (_sync)
            {
                Console.WriteLine(line);
                if (!string.IsNullOrEmpty(FilePath))
                    File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }

        public static void Debug(string msg) => Log(LogLevel.Debug, msg);
        public static void Info(string msg) => Log(LogLevel.Information, msg);
        public static void Warn(string msg) => Log(LogLevel.Warning, msg);
        public static void Error(string msg) => Log(LogLevel.Error, msg);

    }
    public enum LogLevel { Debug = 1, Information = 2, Warning = 3, Error = 4 }

}
