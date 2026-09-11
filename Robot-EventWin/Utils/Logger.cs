using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace CredentialScanner.Utils
{
    public static class Logger
    {
        // ============================================================
        // CONFIGURACIÓN
        // ============================================================
        private const long MaxLogSizeBytes = 10 * 1024 * 1024;   // 10 MB
        private const int MaxBackupFiles = 10;                 // conservar últimos 10 backups
        private static readonly bool RotateByDay = false;         // true = archivo diario

        private static string _logPath;
        private static string _logFileName;
        private static readonly object _lock = new object();

        // ============================================================
        // ESTADO
        // ============================================================
        public static bool IsInitialized =>
            !string.IsNullOrEmpty(_logPath) && !string.IsNullOrEmpty(_logFileName);

        // ============================================================
        // INICIALIZACIÓN
        // ============================================================
        public static void Initialize(string logPath, string logFileName)
        {
            try
            {
                _logPath = logPath;
                _logFileName = logFileName;

                if (!string.IsNullOrEmpty(_logPath) && !Directory.Exists(_logPath))
                    Directory.CreateDirectory(_logPath);
            }
            catch
            {
                // Si falla, dejamos _logPath/_logFileName tal cual.
                // IsInitialized devolverá false si son null/empty y Write no hará nada.
            }
        }

        // ============================================================
        // ESCRITURA
        // ============================================================
        public static void Write(string level, string message, string computerName = "", string userName = "")
        {
            if (!IsInitialized) return;

            try
            {
                lock (_lock)
                {
                    string fullLogPath = GetCurrentLogFilePath();

                    RotateIfNeeded(fullLogPath);

                    string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture);
                    string logEntry = $"{timestamp} | {level.PadRight(7)} | {computerName} | {userName} | {message}{Environment.NewLine}";

                    File.AppendAllText(fullLogPath, logEntry, Encoding.UTF8);
                }
            }
            catch
            {
                // Silencioso a propósito: un logger nunca debe romper la app.
            }
        }

        // ============================================================
        // HELPERS
        // ============================================================
        private static string GetCurrentLogFilePath()
        {
            if (RotateByDay)
            {
                string nameNoExt = Path.GetFileNameWithoutExtension(_logFileName);
                string ext = Path.GetExtension(_logFileName);
                string dailyName = $"{nameNoExt}_{DateTime.Now:yyyyMMdd}{ext}";
                return Path.Combine(_logPath, dailyName);
            }

            return Path.Combine(_logPath, _logFileName);
        }

        private static void RotateIfNeeded(string fullLogPath)
        {
            try
            {
                var fi = new FileInfo(fullLogPath);
                if (!fi.Exists) return;
                if (fi.Length < MaxLogSizeBytes) return;

                string dir = Path.GetDirectoryName(fullLogPath);
                string nameNoExt = Path.GetFileNameWithoutExtension(fullLogPath);
                string ext = Path.GetExtension(fullLogPath);

                string backupName = $"{nameNoExt}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
                string backupPath = Path.Combine(dir, backupName);

                // Renombrar el actual → backup
                File.Move(fullLogPath, backupPath);

                // Purgar backups viejos
                PurgeOldBackups(dir, nameNoExt, ext);
            }
            catch
            {
                // Si la rotación falla, seguimos escribiendo en el archivo actual.
            }
        }

        private static void PurgeOldBackups(string dir, string baseName, string ext)
        {
            try
            {
                var backups = Directory.GetFiles(dir, $"{baseName}_*{ext}")
                                       .Select(f => new FileInfo(f))
                                       .OrderByDescending(f => f.CreationTimeUtc)
                                       .ToList();

                for (int i = MaxBackupFiles; i < backups.Count; i++)
                {
                    try { backups[i].Delete(); } catch { }
                }
            }
            catch { }
        }

        // ============================================================
        // MÉTODOS DE CONVENIENCIA
        // ============================================================
        public static void Info(string message, string computerName = "", string userName = "")
            => Write("INFO", message, computerName, userName);

        public static void Warning(string message, string computerName = "", string userName = "")
            => Write("WARNING", message, computerName, userName);

        public static void Error(string message, string computerName = "", string userName = "")
            => Write("ERROR", message, computerName, userName);

        public static void Alert(string message, string computerName = "", string userName = "")
            => Write("ALERT", message, computerName, userName);

        public static void Debug(string message, string computerName = "", string userName = "")
            => Write("DEBUG", message, computerName, userName);
    }
}