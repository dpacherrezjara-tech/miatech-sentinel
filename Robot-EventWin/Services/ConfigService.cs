using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CredentialScanner.Models;

namespace CredentialScanner.Services
{
    public class ConfigService
    {
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
                    var lowerValue = value.ToLowerInvariant().Trim();
                    if (lowerValue == "true" || lowerValue == "1" || lowerValue == "yes")
                        return (T)(object)true;
                    if (lowerValue == "false" || lowerValue == "0" || lowerValue == "no")
                        return (T)(object)false;
                    return defaultValue;
                }
                if (typeof(T) == typeof(int))
                {
                    return (T)(object)int.Parse(value);
                }
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

        public List<CredentialRule> GetRules()
        {
            var rules = new List<CredentialRule>();
            var ruleKeys = _config.Keys.Where(k => k.StartsWith("SCANNER_RULES:RULES")).ToList();

            if (ruleKeys.Count == 0) return rules;

            foreach (var key in ruleKeys.OrderBy(k => k))
            {
                var rulesRaw = _config[key];
                var parts = rulesRaw.Split('|', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length >= 4)
                {
                    var pattern = string.Join("|", parts.Skip(2).Take(parts.Length - 3));

                    rules.Add(new CredentialRule
                    {
                        Id = parts[0].Trim(),
                        Description = parts[1].Trim(),
                        Pattern = pattern.Trim(),
                        Severity = parts[parts.Length - 1].Trim()
                    });
                }
            }

            return rules;
        }

        public List<string> GetScanPaths()
        {
            var paths = new List<string>();
            var monitorAllDrives = GetValue<bool>("MONITOR_SETTINGS", "MONITOR_ALL_DRIVES", false);
            var monitorSpecificPaths = GetValue("MONITOR_SETTINGS", "MONITOR_SPECIFIC_PATHS", "");

            if (monitorAllDrives)
            {
                var drives = DriveInfo.GetDrives();
                foreach (var drive in drives)
                {
                    if (drive.DriveType == DriveType.Fixed && drive.IsReady)
                    {
                        paths.Add(drive.Name);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(monitorSpecificPaths))
            {
                var pathList = monitorSpecificPaths.Split(';', StringSplitOptions.RemoveEmptyEntries);
                foreach (var path in pathList)
                {
                    var trimmedPath = path.Trim();
                    if (Directory.Exists(trimmedPath))
                    {
                        paths.Add(trimmedPath);
                    }
                }
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
    }
}