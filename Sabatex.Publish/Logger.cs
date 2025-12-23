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
        public static string? FilePath { get; private set; } = null;  // FIXED: nullable + default value
        public static LogLevel MinimumLevel { get; private set; } = LogLevel.Information;  // FIXED: default value

        public static void Initialize(string? filePath, LogLevel minLevel)
        {
            FilePath = filePath;
            MinimumLevel = minLevel;
            if (!string.IsNullOrEmpty(FilePath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.AppendAllText(FilePath, $"=== Log started: {DateTime.Now} ==={Environment.NewLine}");
            }
        }

        public static void Log(LogLevel level, string message, ConsoleColor? color = null)
        {
            if (level < MinimumLevel) return;

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}";
            lock (_sync)
            {
                if (color.HasValue)
                {
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = color.Value;
                    Console.WriteLine(line);
                    Console.ForegroundColor = originalColor;
                }
                else
                {
                    // set default colors based on log level
                    var originalColor = Console.ForegroundColor;
                    Console.ForegroundColor = level switch
                    {
                        LogLevel.Error => ConsoleColor.Red,
                        LogLevel.Warning => ConsoleColor.Yellow,
                        LogLevel.Debug => ConsoleColor.Gray,
                        _ => originalColor
                    };
                    Console.WriteLine(line);
                    Console.ForegroundColor = originalColor;
                }

                if (!string.IsNullOrEmpty(FilePath))
                    File.AppendAllText(FilePath, line + Environment.NewLine);
            }
        }

        public static void Debug(string msg) => Log(LogLevel.Debug, msg);
        
        public static void Info(string msg, bool newLine = true) 
        {
            if (newLine)
                Log(LogLevel.Information, msg);
            else
            {
                lock (_sync)
                {
                    Console.Write($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [Information] {msg}");
                }
            }
        }
        
        public static void Warn(string msg) => Log(LogLevel.Warning, msg);
        public static void Error(string msg) => Log(LogLevel.Error, msg);
        public static void Success(string msg) => Log(LogLevel.Information, msg, ConsoleColor.Green);
    }
    
    public enum LogLevel { Debug = 1, Information = 2, Warning = 3, Error = 4 }
}
