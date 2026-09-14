using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CredentialScanner
{
    class Program
    {
        private static Dictionary<string, string> _config = new();
        private static List<CredentialRule> _rules = new();
        private static string _logPath;
        private static string _logFileName = "CredentialScanner.log";
        private static int _maxFileSizeMB = 10;
        private static List<string> _excludedPaths = new();
        private static List<string> _excludedExtensions = new();
        private static List<string> _scanPaths = new();
        private static List<FileSystemWatcher> _watchers = new();
        private static readonly Dictionary<string, DateTime> _processingQueue = new();
        private static readonly object _lockObject = new();

        static async Task Main(string[] args)
        {
            Console.WriteLine("================================================");
            Console.WriteLine("  CREDENTIAL SCANNER - MODO MONITOR CONTINUO");
            Console.WriteLine("  Escaneo de credenciales en archivos");
            Console.WriteLine("================================================");
            Console.WriteLine();

            string rootSetup = ConfigurationManager.AppSettings["rootSetup"];
            string nameSetup = ConfigurationManager.AppSettings["nameSetup"];

            if (string.IsNullOrEmpty(rootSetup) || string.IsNullOrEmpty(nameSetup))
            {
                Console.WriteLine(" ERROR: Configuración incompleta en App.config");
                Console.WriteLine("   Verifica rootSetup y nameSetup");
                Console.ReadKey();
                return;
            }

            string setupPath = Path.Combine(rootSetup, nameSetup);
            Console.WriteLine($" SETUP.dat: {setupPath}");
            Console.WriteLine();

            LoadConfig(setupPath);

            _maxFileSizeMB = GetConfigValue("SCANNER_SETTINGS", "MAX_FILE_SIZE_MB", 10);
            _logPath = GetConfigValue("LOG_SETTINGS", "LOG_PATH",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "CredentialScanner", "Logs"));
            _logFileName = GetConfigValue("LOG_SETTINGS", "LOG_FILE_NAME", "CredentialScanner.log");
            _excludedPaths = GetConfigList("SCANNER_SETTINGS", "EXCLUDED_PATHS", ';');
            _excludedExtensions = GetConfigList("SCANNER_SETTINGS", "EXCLUDED_EXTENSIONS", ';');
            _rules = GetRulesFromConfig();

            // Leer rutas a escanear
            var monitorAllDrives = GetConfigValue<bool>("MONITOR_SETTINGS", "MONITOR_ALL_DRIVES", false);
            var monitorSpecificPaths = GetConfigValue("MONITOR_SETTINGS", "MONITOR_SPECIFIC_PATHS", "");

            if (args.Length > 0)
            {
                _scanPaths.Add(args[0]);
            }
            else if (monitorAllDrives)
            {
                // 🔴 OBTENER SOLO UNIDADES LOCALES (NO COMPARTIDAS)
                Console.WriteLine("🔍 Obteniendo unidades locales (discos duros)...");

                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    // 🔴 SOLO unidades locales (DriveType.Fixed)
                    if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                    {
                        _scanPaths.Add(drive.Name);
                        Console.WriteLine($"   ✅ Unidad local: {drive.Name} ({drive.VolumeLabel})");
                    }
                    else if (drive.DriveType == DriveType.Network && drive.IsReady)
                    {
                        // 🔴 OPCIONAL: Mostrar unidades de red que NO se escanearán
                        Console.WriteLine($"   ⚠️ Unidad de red ignorada: {drive.Name} ({drive.VolumeLabel})");
                    }
                    else if (drive.DriveType == DriveType.Removable && drive.IsReady)
                    {
                        // 🔴 OPCIONAL: Mostrar unidades removibles que NO se escanearán
                        Console.WriteLine($"   ⚠️ Unidad removible ignorada: {drive.Name} ({drive.VolumeLabel})");
                    }
                    else if (drive.DriveType == DriveType.CDRom && drive.IsReady)
                    {
                        Console.WriteLine($"   ⚠️ Unidad CD/DVD ignorada: {drive.Name}");
                    }
                }

                Console.WriteLine();
            }
            else if (!string.IsNullOrEmpty(monitorSpecificPaths))
            {
                var paths = monitorSpecificPaths.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var path in paths)
                {
                    var trimmedPath = path.Trim();
                    if (Directory.Exists(trimmedPath))
                        _scanPaths.Add(trimmedPath);
                    else
                        Console.WriteLine($" Ruta no existe: {trimmedPath}");
                }
            }

            if (_scanPaths.Count == 0)
            {
                Console.WriteLine(" ERROR: No hay rutas configuradas para escanear.");
                Console.ReadKey();
                return;
            }

            Console.WriteLine($" Configuración cargada");
            Console.WriteLine($" Rutas a monitorear:");
            foreach (var path in _scanPaths)
                Console.WriteLine($"   - {path}");
            Console.WriteLine($" Log: {Path.Combine(_logPath, _logFileName)}");
            Console.WriteLine($" Tamaño máximo: {_maxFileSizeMB}MB");
            Console.WriteLine($" Exclusiones: {_excludedPaths.Count} rutas, {_excludedExtensions.Count} extensiones");
            Console.WriteLine($" Reglas: {_rules.Count}");
            Console.WriteLine();

            foreach (var rule in _rules)
                Console.WriteLine($"   [{rule.Severity}] {rule.Id}: {rule.Description}");

            Console.WriteLine();

            Directory.CreateDirectory(_logPath);

            Console.WriteLine(" REALIZANDO ESCANEO INICIAL DE ARCHIVOS EXISTENTES...");
            Console.WriteLine();

            await ScanAllExistingFiles();

            Console.WriteLine();
            Console.WriteLine(" ESCANEO INICIAL COMPLETADO!");
            Console.WriteLine();

            //  PASO 2: INICIAR MONITOREO CONTINUO
            Console.WriteLine(" INICIANDO MONITOREO CONTINUO...");
            Console.WriteLine("   El sistema escaneará archivos en tiempo real");
            Console.WriteLine("   Presiona 'Q' para salir");
            Console.WriteLine();

            StartMonitoring();

            // Mantener el programa ejecutándose
            while (Console.ReadKey(true).Key != ConsoleKey.Q)
            {
                await Task.Delay(100);
            }

            // Limpiar watchers
            foreach (var watcher in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }

            Console.WriteLine();
            Console.WriteLine(" Monitoreo detenido.");
            Console.WriteLine($" Log: {Path.Combine(_logPath, _logFileName)}");
            Console.WriteLine("Presiona cualquier tecla para salir...");
            Console.ReadKey();
        }

        #region Configuración

        private static void LoadConfig(string configPath)
        {
            if (!File.Exists(configPath))
            {
                Console.WriteLine($" ERROR: No se encuentra {configPath}");
                Console.ReadKey();
                Environment.Exit(1);
            }

            try
            {
                var lines = File.ReadAllLines(configPath);
                string currentSection = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                        continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.TrimStart('[').TrimEnd(']');
                        continue;
                    }

                    if (trimmed.Contains('|'))
                    {
                        var parts = trimmed.Split('|', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2)
                        {
                            var key = parts[0].Trim();
                            var value = parts[1].Trim();
                            if (parts.Length > 2)
                                value = string.Join("|", parts.Skip(1)).Trim();

                            var fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}:{key}";
                            _config[fullKey] = value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error al leer {configPath}: {ex.Message}");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        private static string GetConfigValue(string section, string key, string defaultValue = null)
        {
            var fullKey = $"{section}:{key}";
            return _config.TryGetValue(fullKey, out var value) ? value : defaultValue;
        }

        private static T GetConfigValue<T>(string section, string key, T defaultValue = default)
        {
            var value = GetConfigValue(section, key);
            if (string.IsNullOrEmpty(value))
                return defaultValue;
            try
            {
                if (typeof(T) == typeof(bool))
                {
                    var lowerValue = value.ToLowerInvariant().Trim();
                    if (lowerValue == "true" || lowerValue == "1" || lowerValue == "yes")
                        return (T)(object)true;
                    if (lowerValue == "false" || lowerValue == "0" || lowerValue == "no")
                        return (T)(object)false;
                    return defaultValue;
                }
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }

        private static List<string> GetConfigList(string section, string key, char separator = ';')
        {
            var value = GetConfigValue(section, key);
            if (string.IsNullOrEmpty(value))
                return new List<string>();

            return value.Split(separator)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .ToList();
        }

        //  MÉTODO: Soporta RULES1, RULES2, RULES3...
        private static List<CredentialRule> GetRulesFromConfig()
        {
            var rules = new List<CredentialRule>();

            // Buscar todas las claves que empiezan con "SCANNER_RULES:RULES"
            var ruleKeys = _config.Keys.Where(k => k.StartsWith("SCANNER_RULES:RULES")).ToList();

            Console.WriteLine($"    Encontradas {ruleKeys.Count} claves de reglas");

            if (ruleKeys.Count == 0)
            {
                Console.WriteLine(" No se encontraron reglas en SETUP.dat");
                return rules;
            }

            foreach (var key in ruleKeys.OrderBy(k => k))
            {
                var rulesRaw = _config[key];

                // Dividir por | para obtener las partes
                var parts = rulesRaw.Split('|', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 4)
                {
                    // Reconstruir el pattern (puede contener pipes internos)
                    var pattern = string.Join("|", parts.Skip(2).Take(parts.Length - 3));

                    rules.Add(new CredentialRule
                    {
                        Id = parts[0].Trim(),
                        Description = parts[1].Trim(),
                        Pattern = pattern.Trim(),
                        Severity = parts[parts.Length - 1].Trim()
                    });
                }
                else
                {
                    Console.WriteLine($"  Regla ignorada (formato incorrecto): {rulesRaw}");
                }
            }

            Console.WriteLine($"    Reglas cargadas: {rules.Count}");
            return rules;
        }

        #endregion

        #region Escaneo Inicial de Archivos Existentes

        private static async Task ScanAllExistingFiles()
        {
            var allFindings = new List<CredentialFinding>();
            int totalFiles = 0;
            int processed = 0;

            foreach (var scanPath in _scanPaths)
            {
                if (IsPathExcluded(scanPath))
                {
                    Console.WriteLine($" Ruta excluida: {scanPath}");
                    continue;
                }

                Console.WriteLine($" Escaneando: {scanPath}");

                var files = new List<string>();
                var directories = new Queue<string>();
                directories.Enqueue(scanPath);

                while (directories.Count > 0)
                {
                    var currentDir = directories.Dequeue();

                    if (IsPathExcluded(currentDir))
                        continue;

                    try
                    {
                        foreach (var file in Directory.GetFiles(currentDir))
                        {
                            if (!ShouldExcludeFile(file))
                                files.Add(file);
                        }

                        foreach (var subDir in Directory.GetDirectories(currentDir))
                        {
                            directories.Enqueue(subDir);
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        continue;
                    }
                    catch
                    {
                        continue;
                    }
                }

                totalFiles += files.Count;
                Console.WriteLine($"    Archivos a procesar: {files.Count}");
                Console.WriteLine();

                foreach (var file in files)
                {
                    processed++;
                    if (processed % 100 == 0 || processed == files.Count)
                        Console.Write($"\r Progreso: {processed}/{totalFiles}");

                    var findings = await AnalyzeFileAsync(file);
                    if (findings.Any())
                    {
                        allFindings.AddRange(findings);

                        foreach (var finding in findings)
                        {
                            Console.WriteLine($"\n  [{finding.Severity}] {Path.GetFileName(file)}");
                            Console.WriteLine($"     Línea {finding.LineNumber}: {finding.Secret}");
                        }
                    }
                }
            }

            Console.WriteLine($"\r Procesados: {totalFiles} archivos");
            Console.WriteLine($" Hallazgos totales: {allFindings.Count}");
            Console.WriteLine();

            if (allFindings.Any())
            {
                await GenerateReportAsync(allFindings, "ESCANEO INICIAL");
            }
            else
            {
                await LogInfoAsync("ESCANEO INICIAL: No se encontraron credenciales.");
            }
        }

        #endregion

        #region Monitoreo Continuo

        private static void StartMonitoring()
        {
            foreach (var path in _scanPaths)
            {
                if (IsPathExcluded(path))
                {
                    Console.WriteLine($" Ruta excluida: {path}");
                    continue;
                }

                try
                {
                    var watcher = new FileSystemWatcher
                    {
                        Path = path,
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                        Filter = "*.*",
                        InternalBufferSize = 65536
                    };

                    watcher.Created += OnFileChanged;
                    watcher.Changed += OnFileChanged;
                    watcher.Renamed += OnFileRenamed;
                    watcher.Error += OnWatcherError;

                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);

                    Console.WriteLine($" Monitoreando: {path}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($" Error en {path}: {ex.Message}");
                }
            }

            Console.WriteLine();
            Console.WriteLine($" {_watchers.Count} watchers activos");
        }

        private static async void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.ChangeType == WatcherChangeTypes.Deleted)
                return;

            await ProcessFileAsync(e.FullPath);
        }

        private static async void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            await ProcessFileAsync(e.FullPath);
        }

        private static void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Console.WriteLine($" Error en watcher: {e.GetException().Message}");
        }

        private static async Task ProcessFileAsync(string filePath)
        {
            lock (_lockObject)
            {
                if (_processingQueue.TryGetValue(filePath, out var lastProcessed) &&
                    (DateTime.Now - lastProcessed).TotalSeconds < 2)
                    return;
                _processingQueue[filePath] = DateTime.Now;
            }

            try
            {
                await WaitForFileReady(filePath);

                if (ShouldExcludeFile(filePath))
                    return;

                Console.WriteLine($"\n Archivo detectado: {Path.GetFileName(filePath)}");

                var findings = await AnalyzeFileAsync(filePath);

                if (findings.Any())
                {
                    Console.WriteLine($" ¡{findings.Count} hallazgos en {Path.GetFileName(filePath)}!");

                    foreach (var finding in findings)
                    {
                        Console.WriteLine($"   [{finding.Severity}] Línea {finding.LineNumber}: {finding.Secret}");
                    }

                    await GenerateReportAsync(findings, "MONITOREO EN TIEMPO REAL");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($" Error procesando {filePath}: {ex.Message}");
            }
            finally
            {
                lock (_lockObject)
                {
                    _processingQueue.Remove(filePath);
                }
            }
        }

        private static async Task WaitForFileReady(string filePath, int maxRetries = 5)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(200 * (i + 1));
                }
                catch
                {
                    await Task.Delay(100);
                }
            }
        }

        #endregion

        #region Escaneo

        private static bool IsPathExcluded(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            try
            {
                var normalized = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

                foreach (var excluded in _excludedPaths)
                {
                    if (string.IsNullOrEmpty(excluded))
                        continue;

                    var excludedNormalized = Path.GetFullPath(excluded).TrimEnd(Path.DirectorySeparatorChar);

                    if (normalized.StartsWith(excludedNormalized, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static bool ShouldExcludeFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return true;

            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();

                if (_excludedExtensions.Contains(extension))
                    return true;

                if (IsPathExcluded(filePath))
                    return true;

                var attrs = File.GetAttributes(filePath);
                if ((attrs & FileAttributes.Hidden) == FileAttributes.Hidden ||
                    (attrs & FileAttributes.System) == FileAttributes.System)
                    return true;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > _maxFileSizeMB * 1024 * 1024)
                    return true;

                if (!IsTextFile(filePath))
                    return true;
            }
            catch
            {
                return true;
            }

            return false;
        }

        private static bool IsTextFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var textExtensions = new HashSet<string>
            {
                ".txt", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
                ".py", ".java", ".c", ".cpp", ".h", ".cs", ".vb", ".fs",
                ".sql", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
                ".csv", ".md", ".markdown", ".log", ".env", ".properties",
                ".sh", ".bash", ".ps1", ".bat", ".cmd", ".jsx", ".tsx", ".vue",
                ".config", ".settings", ".xml", ".json", ".yml", ".yaml",
                ".toml", ".ini", ".cfg", ".conf", ".rst", ".pod",
                ".pl", ".pm", ".rb", ".go", ".rs", ".swift", ".kt", ".scala"
            };
            return textExtensions.Contains(ext);
        }

        #endregion

        #region Análisis

        private static async Task<List<CredentialFinding>> AnalyzeFileAsync(string filePath)
        {
            var findings = new List<CredentialFinding>();

            try
            {
                string content = null;

                try
                {
                    content = await File.ReadAllTextAsync(filePath, Encoding.UTF8);
                }
                catch
                {
                    try
                    {
                        content = await File.ReadAllTextAsync(filePath, Encoding.Default);
                    }
                    catch
                    {
                        try
                        {
                            content = await File.ReadAllTextAsync(filePath, Encoding.ASCII);
                        }
                        catch
                        {
                            return findings;
                        }
                    }
                }

                if (string.IsNullOrEmpty(content))
                    return findings;

                foreach (var rule in _rules)
                {
                    try
                    {
                        var matches = Regex.Matches(content, rule.Pattern,
                            RegexOptions.IgnoreCase | RegexOptions.Multiline);

                        foreach (Match match in matches)
                        {
                            string secret = match.Value;
                            for (int i = match.Groups.Count - 1; i >= 1; i--)
                            {
                                if (!string.IsNullOrEmpty(match.Groups[i].Value))
                                {
                                    secret = match.Groups[i].Value;
                                    break;
                                }
                            }

                            if (string.IsNullOrEmpty(secret) || secret.Length < 4)
                                continue;

                            findings.Add(new CredentialFinding
                            {
                                FilePath = filePath,
                                RuleId = rule.Id,
                                Description = rule.Description,
                                Severity = rule.Severity,
                                Secret = secret,
                                LineNumber = GetLineNumber(content, match.Index),
                                FullMatch = match.Value
                            });
                        }
                    }
                    catch { }
                }
            }
            catch { }

            return findings;
        }

        private static int GetLineNumber(string content, int index)
        {
            if (index <= 0) return 1;
            return content.AsSpan(0, index).ToString().Split('\n').Length;
        }

        #endregion

        #region Reportes

        private static async Task GenerateReportAsync(List<CredentialFinding> findings, string tipo = "REPORTE")
        {
            var fullLogPath = Path.Combine(_logPath, _logFileName);

            var sb = new StringBuilder();
            sb.AppendLine("================================================");
            sb.AppendLine($"  CREDENTIAL SCANNER - {tipo}");
            sb.AppendLine($"  Fecha: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Total: {findings.Count}");
            sb.AppendLine("================================================");
            sb.AppendLine();

            var grouped = findings.GroupBy(f => f.Severity);
            foreach (var group in grouped)
                sb.AppendLine($"  {group.Key}: {group.Count()}");

            sb.AppendLine();
            sb.AppendLine("DETALLE:");
            sb.AppendLine("═══════════════════════════════════════════════════════════════");
            sb.AppendLine();

            int count = 0;
            foreach (var finding in findings.OrderByDescending(f => f.Severity))
            {
                count++;
                sb.AppendLine($"#{count}");
                sb.AppendLine($"   Ruta: {finding.FilePath}");
                sb.AppendLine($"   Regla: {finding.RuleId}");
                sb.AppendLine($"   Descripción: {finding.Description}");
                sb.AppendLine($"   Severidad: {finding.Severity}");
                sb.AppendLine($"   Secreto: {finding.Secret}");
                //sb.AppendLine($"   Línea: {finding.LineNumber}");
                sb.AppendLine($"   Match: {finding.FullMatch}");
                sb.AppendLine($"   Detectado: {finding.DetectedAt:yyyy-MM-dd HH:mm:ss}");
                sb.AppendLine($"   {'─'}");
                sb.AppendLine();
            }

            sb.AppendLine("================================================");
            sb.AppendLine($"  FIN - {findings.Count} hallazgos");
            sb.AppendLine("================================================");

            await File.AppendAllTextAsync(fullLogPath, sb.ToString(), Encoding.UTF8);
            Console.WriteLine($" Reporte guardado en: {fullLogPath}");

            /*var csvPath = Path.Combine(_logPath, $"CredentialScanner_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            var csv = new StringBuilder();
            csv.AppendLine("Fecha,Archivo,Regla,Descripcion,Severidad,Secreto,Linea,Match");

            foreach (var finding in findings)
            {
                csv.AppendLine($"{finding.DetectedAt:yyyy-MM-dd HH:mm:ss}," +
                             $"\"{finding.FilePath.Replace("\"", "\"\"")}\"," +
                             $"{finding.RuleId}," +
                             $"\"{finding.Description.Replace("\"", "\"\"")}\"," +
                             $"{finding.Severity}," +
                             $"\"{finding.Secret.Replace("\"", "\"\"")}\"," +
                             $"{finding.LineNumber}," +
                             $"\"{finding.FullMatch.Replace("\"", "\"\"")}\"");
            }

            await File.WriteAllTextAsync(csvPath, csv.ToString(), Encoding.UTF8);
            Console.WriteLine($" CSV guardado: {csvPath}");*/
        }

        private static async Task LogInfoAsync(string message)
        {
            var fullLogPath = Path.Combine(_logPath, _logFileName);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}";
            await File.AppendAllTextAsync(fullLogPath, line + Environment.NewLine, Encoding.UTF8);
        }

        #endregion
    }

    #region Clases

    public class CredentialRule
    {
        public string Id { get; set; }
        public string Description { get; set; }
        public string Pattern { get; set; }
        public string Severity { get; set; }
    }

    public class CredentialFinding
    {
        public string FilePath { get; set; }
        public string RuleId { get; set; }
        public string Description { get; set; }
        public string Severity { get; set; }
        public string Secret { get; set; }
        public int LineNumber { get; set; }
        public string FullMatch { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.Now;
    }

    #endregion
}