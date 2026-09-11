using CredentialScanner.Models;
using CredentialScanner.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CredentialScanner.Services
{
    public class CredentialScannerService
    {
        private readonly List<CredentialRule> _rules;
        private readonly List<string> _excludedPaths;
        private readonly List<string> _excludedExtensions;
        private readonly int _maxFileSizeMB;
        private readonly RiskScoringService _riskScoring;
        private readonly int _umbralLog;

        public CredentialScannerService(ConfigService config, string computerName, string userName)
        {
            _rules = config.GetRules();
            _excludedPaths = config.ExcludedPaths;
            _excludedExtensions = config.ExcludedExtensions;
            _maxFileSizeMB = config.MaxFileSizeMB;
            _riskScoring = new RiskScoringService(config);
            _umbralLog = config.GetValue<int>("RISK_SCORING", "UMBRAL_LOG", 80);
        }

        public async Task<List<CredentialFinding>> AnalyzeFileAsync(string filePath)
        {
            var findings = new List<CredentialFinding>();

            try
            {
                string content = await ReadFileAsync(filePath);
                if (string.IsNullOrEmpty(content)) return findings;

                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);
                var lineasAgregadas = new HashSet<int>();

                // 🔴 DIAGNÓSTICO: Cuántas líneas se van a analizar
                //Logger.Info($"DIAGNÓSTICO: Archivo {Path.GetFileName(filePath)} tiene {lines.Length} líneas", "", "");

                // 🔴 1. PUNTUACIÓN DE RIESGO
                var riskScores = _riskScoring.AnalyzeContent(content, filePath);

              //  Logger.Info($"DIAGNÓSTICO: AnalyzeContent encontró {riskScores.Count} hallazgos", "", "");

                foreach (var risk in riskScores)
                {
                    //Logger.Info($"DIAGNÓSTICO: Riesgo Línea {risk.LineNumber} ({risk.Score} pts): {risk.Line}", "", "");

                    findings.Add(new CredentialFinding
                    {
                        FilePath = filePath,
                        RuleId = risk.RuleId,
                        Description = $"{risk.Description} ({risk.Score} pts)",
                        Severity = risk.Severity,
                        Secret = risk.Line,
                        LineNumber = risk.LineNumber,
                        FullMatch = risk.Line
                    });

                    lineasAgregadas.Add(risk.LineNumber);
                }

                // 🔴 2. REGLAS ESPECÍFICAS
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

                            if (string.IsNullOrEmpty(secret) || secret.Length < 4) continue;

                            int lineNumber = GetLineNumber(content, match.Index);

                            if (lineasAgregadas.Contains(lineNumber))
                                continue;

                            if (findings.Any(f => f.LineNumber == lineNumber && f.Secret == secret))
                                continue;

                            //  CALCULAR PUNTUACIÓN
                            string lineContent = lineNumber >= 1 && lineNumber <= lines.Length
                                ? lines[lineNumber - 1].Trim()
                                : secret;

                            var riskScore = _riskScoring.CalcularPuntuacion(lineContent, filePath);

                            //  DIAGNÓSTICO
                           // Logger.Info($"DIAGNÓSTICO: Regla {rule.Id} Línea {lineNumber} → {riskScore.Score} pts (umbral: {_umbralLog})", "", "");

                            //  SOLO AGREGAR SI SUPERA EL UMBRAL
                            if (riskScore.Score < _umbralLog)
                                continue;

                            findings.Add(new CredentialFinding
                            {
                                FilePath = filePath,
                                RuleId = rule.Id,
                                Description = $"{rule.Description} ({riskScore.Score} pts)",
                                Severity = rule.Severity,
                                Secret = secret,
                                LineNumber = lineNumber,
                                FullMatch = match.Value
                            });

                            lineasAgregadas.Add(lineNumber);
                        }
                    }
                    catch { }
                }

                //Logger.Info($"DIAGNÓSTICO: Total hallazgos: {findings.Count}", "", "");
            }
            catch (Exception ex)
            {
                Logger.Error($"DIAGNÓSTICO ERROR: {ex.Message}", "", "");
            }

            return findings;
        }
        public bool ShouldExcludeFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return true;

            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                if (_excludedExtensions.Contains(extension)) return true;
                if (IsPathExcluded(filePath)) return true;

                var attrs = File.GetAttributes(filePath);
                if ((attrs & FileAttributes.Hidden) == FileAttributes.Hidden ||
                    (attrs & FileAttributes.System) == FileAttributes.System)
                    return true;

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > _maxFileSizeMB * 1024 * 1024) return true;

                if (!IsTextFile(filePath)) return true;
            }
            catch { return true; }

            return false;
        }

        private bool IsPathExcluded(string path)
        {
            if (string.IsNullOrEmpty(path)) return true;

            try
            {
                var normalizedPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar);

                foreach (var excluded in _excludedPaths)
                {
                    if (string.IsNullOrEmpty(excluded)) continue;

                    var excludedNormalized = Path.GetFullPath(excluded).TrimEnd(Path.DirectorySeparatorChar);

                    if (normalizedPath.StartsWith(excludedNormalized, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
            catch { return true; }

            return false;
        }

        private bool IsTextFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var textExtensions = new HashSet<string>
            {
                ".txt", ".json", ".xml", ".html", ".htm", ".css", ".js", ".ts",
                ".py", ".java", ".c", ".cpp", ".h", ".cs", ".vb", ".fs",
                ".sql", ".yaml", ".yml", ".toml", ".ini", ".cfg", ".conf",
                ".csv", ".md", ".markdown", ".log", ".env", ".properties",
                ".sh", ".bash", ".ps1", ".bat", ".cmd", ".jsx", ".tsx", ".vue",
                ".config", ".settings", ".rst", ".pod",
                ".pl", ".pm", ".rb", ".go", ".rs", ".swift", ".kt", ".scala"
            };
            return textExtensions.Contains(ext);
        }

        public async Task WaitForFileReady(string filePath, int maxRetries = 5)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return;
                }
                catch (IOException) { await Task.Delay(200 * (i + 1)); }
                catch { await Task.Delay(100); }
            }
        }

        private async Task<string> ReadFileAsync(string filePath)
        {
            try { return await File.ReadAllTextAsync(filePath, Encoding.UTF8); }
            catch { try { return await File.ReadAllTextAsync(filePath, Encoding.Default); } catch { try { return await File.ReadAllTextAsync(filePath, Encoding.ASCII); } catch { return null; } } }
        }

        private int GetLineNumber(string content, int index)
        {
            if (index <= 0) return 1;
            return content.AsSpan(0, index).ToString().Split('\n').Length;
        }
    }
}