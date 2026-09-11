using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CredentialScanner.Models;

namespace CredentialScanner.Services
{
    public class CredentialScannerService
    {
        private readonly List<CredentialRule> _rules;
        private readonly List<string> _excludedPaths;
        private readonly List<string> _excludedExtensions;
        private readonly int _maxFileSizeMB;

        public CredentialScannerService(ConfigService config, string computerName, string userName)
        {
            _rules = config.GetRules();
            _excludedPaths = config.ExcludedPaths;
            _excludedExtensions = config.ExcludedExtensions;
            _maxFileSizeMB = config.MaxFileSizeMB;
        }

        public async Task<List<CredentialFinding>> AnalyzeFileAsync(string filePath)
        {
            var findings = new List<CredentialFinding>();

            try
            {
                string content = null;

                try { content = await File.ReadAllTextAsync(filePath, Encoding.UTF8); }
                catch { try { content = await File.ReadAllTextAsync(filePath, Encoding.Default); } catch { try { content = await File.ReadAllTextAsync(filePath, Encoding.ASCII); } catch { return findings; } } }

                if (string.IsNullOrEmpty(content)) return findings;

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
                ".config", ".settings", ".xml", ".json", ".yml", ".yaml",
                ".toml", ".ini", ".cfg", ".conf", ".rst", ".pod",
                ".pl", ".pm", ".rb", ".go", ".rs", ".swift", ".kt", ".scala"
            };
            return textExtensions.Contains(ext);
        }

        private int GetLineNumber(string content, int index)
        {
            if (index <= 0) return 1;
            return content.AsSpan(0, index).ToString().Split('\n').Length;
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
    }
}