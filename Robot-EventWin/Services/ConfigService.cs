using CredentialScanner.Models;
using CredentialScanner.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CredentialScanner.Services
{
    public class ConfigService
    {
        private const char FieldSeparator = '§';

        private readonly Dictionary<string, string> _config = new();

        public ConfigService(string configPath)
        {
            if (!File.Exists(configPath))
                throw new FileNotFoundException($"Archivo de configuración no encontrado: {configPath}");

            LoadConfig(configPath);
        }

        private void LoadConfig(string configPath)
        {
            try
            {
                var lines = File.ReadAllLines(configPath);
                string currentSection = null;

                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    trimmed = trimmed.TrimStart('\uFEFF'); // Eliminar BOM

                    if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#"))
                        continue;

                    if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
                    {
                        currentSection = trimmed.TrimStart('[').TrimEnd(']').Trim();
                        continue;
                    }

                    // 🔴 TODAS las líneas usan § como separador
                    int sepIdx = trimmed.IndexOf(FieldSeparator);
                    if (sepIdx <= 0) continue;

                    var key = trimmed.Substring(0, sepIdx).Trim();
                    var value = trimmed.Substring(sepIdx + 1).Trim();

                    if (string.IsNullOrEmpty(key)) continue;

                    var fullKey = string.IsNullOrEmpty(currentSection) ? key : $"{currentSection}:{key}";
                    _config[fullKey] = value;
                }
            }
            catch { }
        }

        public string GetValue(string section, string key, string defaultValue = null)
        {
            var fullKey = $"{section}:{key}";
            return _config.TryGetValue(fullKey, out var value) ? value : defaultValue;
        }

        public T GetValue<T>(string section, string key, T defaultValue = default)
        {
            var value = GetValue(section, key, null);
            if (string.IsNullOrEmpty(value)) return defaultValue;

            try
            {
                if (typeof(T) == typeof(bool))
                {
                    var lower = value.ToLowerInvariant().Trim();
                    if (lower == "true" || lower == "1" || lower == "yes" || lower == "si" || lower == "sí") return (T)(object)true;
                    if (lower == "false" || lower == "0" || lower == "no") return (T)(object)false;
                    return defaultValue;
                }

                if (typeof(T) == typeof(int))
                    return (T)(object)int.Parse(value);

                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch { return defaultValue; }
        }

        public List<string> GetList(string section, string key, char separator = ';')
        {
            var value = GetValue(section, key, "");
            if (string.IsNullOrEmpty(value)) return new List<string>();

            return value.Split(separator)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrEmpty(x))
                        .ToList();
        }

        // ============================================================
        // REGLAS
        // ============================================================
        public List<CredentialRule> GetRules()
        {
            var rules = new List<CredentialRule>();
            var ruleKeys = _config.Keys
                .Where(k => k.StartsWith("SCANNER_RULES:RULES"))
                .ToList();

            if (ruleKeys.Count == 0) return rules;

            foreach (var key in ruleKeys.OrderBy(k => k))
            {
                var rulesRaw = _config[key];
                var parts = rulesRaw.Split(FieldSeparator);

                if (parts.Length < 4) continue;

                string id, description, pattern, severity, filterType;

                if (parts.Length == 4)
                {
                    id = parts[0].Trim();
                    description = parts[1].Trim();
                    pattern = parts[2].Trim();
                    severity = parts[3].Trim();
                    filterType = null;
                }
                else if (parts.Length == 5)
                {
                    id = parts[0].Trim();
                    description = parts[1].Trim();
                    pattern = parts[2].Trim();
                    severity = parts[3].Trim();
                    filterType = parts[4].Trim();
                }
                else
                {
                    id = parts[0].Trim();
                    description = parts[1].Trim();
                    pattern = string.Join(FieldSeparator.ToString(), parts.Skip(2).Take(parts.Length - 4)).Trim();
                    severity = parts[parts.Length - 2].Trim();
                    filterType = parts[parts.Length - 1].Trim();
                }

                if (string.IsNullOrEmpty(pattern)) continue;

                rules.Add(new CredentialRule
                {
                    Id = id,
                    Description = description,
                    Pattern = pattern,
                    Severity = severity,
                    FilterType = string.IsNullOrEmpty(filterType) ? null : filterType
                });
            }

            return rules;
        }

        // ============================================================
        // RUTAS
        // ============================================================
        public List<string> GetScanPaths()
        {
            var paths = new List<string>();

            var enabled = GetValue<bool>("MONITOR_SETTINGS", "ENABLE_MONITORING", true);
            if (!enabled)
            {
                Logger.Warning("MONITOR_SETTINGS:ENABLE_MONITORING = false → monitoreo deshabilitado");
                return paths;
            }

            var monitorAllDrives = GetValue<bool>("MONITOR_SETTINGS", "MONITOR_ALL_DRIVES", false);
            var monitorSpecificPaths = GetValue("MONITOR_SETTINGS", "MONITOR_SPECIFIC_PATHS", "");

            if (monitorAllDrives)
            {
                try
                {
                    foreach (var drive in DriveInfo.GetDrives())
                    {
                        if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                            paths.Add(drive.Name);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Error enumerando discos: {ex.Message}");
                }
            }
            else if (!string.IsNullOrEmpty(monitorSpecificPaths))
            {
                foreach (var path in monitorSpecificPaths.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmedPath = path.Trim();
                    if (string.IsNullOrEmpty(trimmedPath)) continue;

                    if (Directory.Exists(trimmedPath))
                        paths.Add(trimmedPath);
                    else
                        Logger.Warning($"Ruta configurada NO existe: {trimmedPath}");
                }
            }
            else
            {
                Logger.Warning("MONITOR_SETTINGS: ni MONITOR_ALL_DRIVES ni MONITOR_SPECIFIC_PATHS están configurados");
            }

            return paths;
        }

        public bool AutoStart => GetValue<bool>("MONITOR_SETTINGS", "AUTO_START", false);
        public int MaxFileSizeMB => GetValue<int>("SCANNER_SETTINGS", "MAX_FILE_SIZE_MB", 10);
        public List<string> ExcludedPaths => GetList("SCANNER_SETTINGS", "EXCLUDED_PATHS", ';');
        public List<string> ExcludedExtensions => GetList("SCANNER_SETTINGS", "EXCLUDED_EXTENSIONS", ';');

        public string LogPath => GetValue("LOG_SETTINGS", "LOG_PATH",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "Miatech Sentinel", "Logs"));

        public string LogFileName => GetValue("LOG_SETTINGS", "LOG_FILE_NAME", "MiatechSentinel.log");
        public string LogLevel => GetValue("LOG_SETTINGS", "LOG_LEVEL", "Info");
        public bool LogMaskSecrets => GetValue<bool>("LOG_SETTINGS", "LOG_MASK_SECRETS", true);
        public bool LogIncludeHostInfo => GetValue<bool>("LOG_SETTINGS", "LOG_INCLUDE_HOST_INFO", false);
    }
}