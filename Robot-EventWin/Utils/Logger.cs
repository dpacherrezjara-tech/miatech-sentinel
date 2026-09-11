using System;
using System.IO;
using System.Text;

namespace CredentialScanner.Utils
{
    public static class Logger
    {
        private static string _logPath;
        private static string _logFileName;
        private static bool _initialized = false;

        public static void Initialize(string logPath, string logFileName)
        {
            try
            {
                // Si no hay ruta, usar una por defecto
                if (string.IsNullOrEmpty(logPath))
                {
                    logPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "Miatech Sentinel", "Logs");
                }

                if (string.IsNullOrEmpty(logFileName))
                    logFileName = "MiatechSentinel.log";

                _logPath = logPath;
                _logFileName = logFileName;

                // 🔴 CREAR LA CARPETA SI NO EXISTE
                if (!Directory.Exists(_logPath))
                {
                    Directory.CreateDirectory(_logPath);
                }

                _initialized = true;

                // Escribir una línea de prueba
                Write("INFO", "═══════════════════════════════════════");
                Write("INFO", "Logger inicializado correctamente");
                Write("INFO", $"Ruta: {Path.Combine(_logPath, _logFileName)}");
                Write("INFO", "═══════════════════════════════════════");
            }
            catch (Exception ex)
            {
                // 🔴 MOSTRAR ERROR SI FALLA
                System.Windows.Forms.MessageBox.Show(
                    $"Error al inicializar Logger:\n\n{ex.Message}\n\nRuta: {logPath}",
                    "Error de Logger",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        public static void Write(string level, string message, string computerName = "", string userName = "")
        {
            if (!_initialized)
            {
                // Si no está inicializado, mostrar error
                System.Windows.Forms.MessageBox.Show(
                    "Logger NO inicializado. Llama a Logger.Initialize() primero.",
                    "Error de Logger",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string fullLogPath = Path.Combine(_logPath, _logFileName);
                string timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz");
                string logEntry = $"{timestamp} | {level.PadRight(7)} | {computerName} | {userName} | {message}{Environment.NewLine}";
                File.AppendAllText(fullLogPath, logEntry, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                // 🔴 MOSTRAR ERROR SI FALLA
                System.Windows.Forms.MessageBox.Show(
                    $"Error escribiendo log:\n\n{ex.Message}\n\nRuta: {Path.Combine(_logPath, _logFileName)}",
                    "Error de Logger",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
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