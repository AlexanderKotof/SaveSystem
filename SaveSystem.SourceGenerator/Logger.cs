using System;
using System.IO;

namespace SaveDataGenerator
{
    public static class Logger
    {
        private static bool _enabled;

        private static string GetLogPath()
        {
            try
            {
                return Path.GetFullPath(Path.Combine("Assets", "Generator", "SourceGen_Debug.log"));
            }
            catch
            {
                return Path.Combine(Path.GetTempPath(), "Unity_SourceGen_Debug.log");
            }
        }

        public static void Log(string message)
        {
            if (!_enabled)
                return;

            try
            {
                var logPath = GetLogPath();
                var dir = Path.GetDirectoryName(logPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}");
                Console.WriteLine(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveGen LogFail] {ex.Message} | {message}");
            }
        }

        public static void Disable()
        {
            _enabled = false;
        }
    }
}
