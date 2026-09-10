using System;
using System.IO;
using System.Text;

namespace CredentialScanner.Utils
{
    public static class Logger
    {
        private static string _logPath;
        private static string _logFileName;

        public static void Initialize(string logPath, string logFileName)
        {
            _logPath = logPath;
            _logFileName = logFileName;

            if (!Directory.Exists(_logPath))
                Directory.CreateDirectory(_logPath);
        }

        public static void Write(string level, string message, string computerName = "", string userName = "")
        {
            try
            {
                string fullLogPath = Path.Combine(_logPath, _logFileName);
                string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                string logEntry = $"{timestamp} | {level.PadRight(7)} | {computerName} | {userName} | {message}{Environment.NewLine}";
                File.AppendAllText(fullLogPath, logEntry, Encoding.UTF8);
            }
            catch { }
        }

        public static void Info(string message, string computerName = "", string userName = "")
            => Write("INFO", message, computerName, userName);

        public static void Warning(string message, string computerName = "", string userName = "")
            => Write("WARNING", message, computerName, userName);

        public static void Error(string message, string computerName = "", string userName = "")
            => Write("ERROR", message, computerName, userName);

        public static void Alert(string message, string computerName = "", string userName = "")
            => Write("ALERT", message, computerName, userName);
    }
}