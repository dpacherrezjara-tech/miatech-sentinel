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

                // 1. REGLAS ESPECÍFICAS PRIMERO
                // Permite capturar el secreto específico y asociar el ID de la regla exacta
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

                            if (string.IsNullOrEmpty(secret) || secret.Length < 3) continue;

                            int lineNumber = GetLineNumber(content, match.Index);

                            if (lineasAgregadas.Contains(lineNumber))
                                continue;

                            if (findings.Any(f => f.LineNumber == lineNumber && f.Secret == secret))
                                continue;

                            string lineContent = lineNumber >= 1 && lineNumber <= lines.Length
                                ? lines[lineNumber - 1].Trim()
                                : secret;

                            if (EsFalsoPositivo(secret, lineContent, filePath))
                                continue;

                            var riskScore = _riskScoring.CalcularPuntuacionConContexto(lineContent, lineNumber, filePath, lines);
                            int finalScore = Math.Max(riskScore.Score, _umbralLog);

                            findings.Add(new CredentialFinding
                            {
                                FilePath = filePath,
                                RuleId = rule.Id,
                                Description = $"{rule.Description} ({finalScore} pts)",
                                Severity = rule.Severity,
                                Secret = secret,
                                LineNumber = lineNumber,
                                FullMatch = match.Value,
                                Score = finalScore
                            });

                            lineasAgregadas.Add(lineNumber);
                        }
                    }
                    catch { }
                }

                // 2. HEURÍSTICA / PUNTUACIÓN DE RIESGO PARA LÍNEAS RESTANTES
                var riskScores = _riskScoring.AnalyzeContent(content, filePath);

                foreach (var risk in riskScores)
                {
                    if (lineasAgregadas.Contains(risk.LineNumber))
                        continue;

                    string candidateSecret = _riskScoring.ExtractCandidateSecret(risk.Line);
                    if (string.IsNullOrEmpty(candidateSecret))
                        candidateSecret = risk.Line;

                    if (EsFalsoPositivo(candidateSecret, risk.Line, filePath))
                        continue;

                    findings.Add(new CredentialFinding
                    {
                        FilePath = filePath,
                        RuleId = risk.RuleId,
                        Description = $"{risk.Description} ({risk.Score} pts)",
                        Severity = risk.Severity,
                        Secret = candidateSecret,
                        LineNumber = risk.LineNumber,
                        FullMatch = risk.Line,
                        Score = risk.Score
                    });

                    lineasAgregadas.Add(risk.LineNumber);
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error analizando archivo {Path.GetFileName(filePath)}: {ex.Message}", "", "");
            }

            return findings.OrderBy(f => f.LineNumber).ToList();
        }

        private bool EsFalsoPositivo(string secret, string lineContent, string filePath)
        {
            if (string.IsNullOrWhiteSpace(secret)) return true;

            string s = secret.Trim();
            string line = lineContent.Trim();

            // 1. Longitud mínima
            if (s.Length < 3) return true;

            // 2. Placeholders y expresiones de plantilla: {keyword}, ${var}, <placeholder>, %param%
            if ((s.StartsWith("{") && s.EndsWith("}")) ||
                (s.StartsWith("<") && s.EndsWith(">")) ||
                (s.StartsWith("$") && s.EndsWith("}")) ||
                (s.StartsWith("%") && s.EndsWith("%")))
                return true;

            // 3. Llamadas a métodos o funciones en código (ej: config.get, os.getenv, get(...))
            if (s.EndsWith("(") || s.Contains("(") || s.Contains(")"))
                return true;

            if (Regex.IsMatch(s, @"^(?:config|os|sys|env|settings|request|params|dict|self|this)\.", RegexOptions.IgnoreCase))
                return true;

            if (Regex.IsMatch(s, @"\.(?:get|getattr|getenv|value|text|string|fetch|read|post|put|delete)\b", RegexOptions.IgnoreCase))
                return true;

            // 4. Si en la línea después del secreto inmediatamente sigue '(' es una llamada a función
            if (Regex.IsMatch(line, Regex.Escape(s) + @"\s*\("))
                return true;

            // 5. Palabras clave neutras / código / valores booleanos o nulos
            var palabrasInvalidas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "none", "null", "undefined", "true", "false", "empty", "default", "dummy", "test",
                "string", "integer", "boolean", "object", "array", "dict", "keyword", "value",
                "password", "usuario", "username", "passwd", "secret", "token", "auth", "cred"
            };
            if (palabrasInvalidas.Contains(s))
                return true;

            // 6. En archivos de código (.py, .js, .ts, .cs, .java, .go, .cpp, etc.)
            // Una asignación a un identificador sin comillas (ej: repo_url_with_creds) es una variable de código
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            var codeExtensions = new HashSet<string> { ".py", ".cs", ".js", ".ts", ".java", ".cpp", ".c", ".h", ".go", ".rb", ".php" };
            if (codeExtensions.Contains(ext))
            {
                bool estaEntreComillas = line.Contains($"\"{s}\"") || line.Contains($"'{s}'");
                if (!estaEntreComillas)
                {
                    // Variables en snake_case minúsculas sin números (ej: repo_url_with_creds)
                    if (Regex.IsMatch(s, @"^[a-z_][a-z0-9_]*$") && !s.Any(char.IsDigit))
                        return true;

                    if (s.Contains("."))
                        return true;
                }
            }

            return false;
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