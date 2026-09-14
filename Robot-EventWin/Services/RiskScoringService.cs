using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CredentialScanner.Models;

namespace CredentialScanner.Services
{
    public class RiskScoringService
    {
        private readonly List<string> _palabrasComunes;
        private readonly List<string> _archivosSospechosos;
        private readonly int _umbralInfo;
        private readonly int _umbralWarning;
        private readonly int _umbralAlert;
        private readonly int _umbralLog;

        private readonly int _puntosEtiqueta;
        private readonly int _puntosFormatoContrasena;
        private readonly int _puntosSimbolo;
        private readonly int _puntosLongitud;
        private readonly int _puntosArchivoSospechoso;
        private readonly int _puntosBloque;
        private readonly int _puntosNoPalabraComun;

        public RiskScoringService(ConfigService config)
        {
            _palabrasComunes = config.GetList("RISK_SCORING", "PALABRAS_COMUNES", ';')
                .Select(p => p.ToLowerInvariant().Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            _archivosSospechosos = config.GetList("RISK_SCORING", "ARCHIVOS_SOSPECHOSOS", ';')
                .Select(p => p.ToLowerInvariant().Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();

            _umbralInfo = config.GetValue<int>("RISK_SCORING", "UMBRAL_INFO", 40);
            _umbralWarning = config.GetValue<int>("RISK_SCORING", "UMBRAL_WARNING", 70);
            _umbralAlert = config.GetValue<int>("RISK_SCORING", "UMBRAL_ALERT", 100);
            _umbralLog = config.GetValue<int>("RISK_SCORING", "UMBRAL_LOG", 80);

            _puntosEtiqueta = config.GetValue<int>("RISK_SCORING", "PUNTOS_ETIQUETA", 50);
            _puntosFormatoContrasena = config.GetValue<int>("RISK_SCORING", "PUNTOS_FORMATO_CONTRASENA", 30);
            _puntosSimbolo = config.GetValue<int>("RISK_SCORING", "PUNTOS_SIMBOLO", 20);
            _puntosLongitud = config.GetValue<int>("RISK_SCORING", "PUNTOS_LONGITUD", 10);
            _puntosArchivoSospechoso = config.GetValue<int>("RISK_SCORING", "PUNTOS_ARCHIVO_SOSPECHOSO", 30);
            _puntosBloque = config.GetValue<int>("RISK_SCORING", "PUNTOS_BLOQUE", 30);
            _puntosNoPalabraComun = config.GetValue<int>("RISK_SCORING", "PUNTOS_NO_PALABRA_COMUN", 10);
        }

        // ============================================================
        // MÉTODO PRINCIPAL: ANALIZAR CONTENIDO COMPLETO
        // ============================================================
        public List<RiskScore> AnalyzeContent(string content, string filePath)
        {
            var results = new List<RiskScore>();
            var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.Length < 6 || line.Length > 200) continue;

                // Rechazar líneas que no son credenciales
                if (EsLineaNoCredencial(line)) continue;

                var score = CalculateLineScore(line, i + 1, filePath, lines);

                if (score.Score >= _umbralLog)
                {
                    results.Add(score);
                }
            }

            return results;
        }

        // ============================================================
        // MÉTODO PÚBLICO: CALCULAR PUNTUACIÓN DE UNA LÍNEA (SIN CONTEXTO)
        // ============================================================
        public RiskScore CalcularPuntuacion(string line, string filePath)
        {
            return CalculateLineScore(line, 0, filePath, new[] { line });
        }

        // ============================================================
        // MÉTODO PÚBLICO: CALCULAR PUNTUACIÓN CON CONTEXTO
        // ============================================================
        public RiskScore CalcularPuntuacionConContexto(string line, int lineNumber, string filePath, string[] allLines)
        {
            return CalculateLineScore(line, lineNumber, filePath, allLines);
        }

        // ============================================================
        // MÉTODO PÚBLICO: CALCULAR PUNTUACIÓN DE UNA LÍNEA
        // ============================================================
        public RiskScore CalculateLineScore(string line, int lineNumber, string filePath, string[] allLines)
        {
            var score = new RiskScore
            {
                Line = line,
                LineNumber = lineNumber,
                Score = 0,
                Reasons = new List<string>()
            };

            if (string.IsNullOrEmpty(line))
                return score;

            // 0. Extraer token candidato de secreto
            string candidate = ExtractCandidateSecret(line);
            if (string.IsNullOrEmpty(candidate) || EsFalsoPositivoToken(candidate, line))
                return score;

            // 1. ¿Tiene etiqueta explícita? (Soporta separadores :, =, ->, =>, |)
            if (Regex.IsMatch(line, @"(?i)\b(?:usuario|user(?:name)?|contraseña|password|pass|clave|pwd|u|c)\b\s*(?:[:=]|->|=>|\|)\s*\S+") &&
                !Regex.IsMatch(candidate, @"^(?:config|os|sys|env|settings|request|self|this)\.", RegexOptions.IgnoreCase))
            {
                score.Score += _puntosEtiqueta;
                score.Reasons.Add($"Etiqueta explícita (+{_puntosEtiqueta})");
            }

            // 2. ¿Tiene formato de contraseña? (mayúscula + minúscula + número en el valor)
            if (HasPasswordFormat(candidate))
            {
                score.Score += _puntosFormatoContrasena;
                score.Reasons.Add($"Formato de contraseña (+{_puntosFormatoContrasena})");
            }

            // 3. ¿Tiene símbolos en el valor?
            if (Regex.IsMatch(candidate, @"[@#$%^&*!_.\-+~`=\\/|]"))
            {
                score.Score += _puntosSimbolo;
                score.Reasons.Add($"Tiene símbolos (+{_puntosSimbolo})");
            }

            // 4. ¿Longitud sospechosa del valor (8-32)?
            if (candidate.Length >= 8 && candidate.Length <= 32)
            {
                score.Score += _puntosLongitud;
                score.Reasons.Add($"Longitud sospechosa (+{_puntosLongitud})");
            }

            // 5. ¿Archivo sospechoso?
            var fileName = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
            if (_archivosSospechosos.Any(s => fileName.Contains(s)))
            {
                score.Score += _puntosArchivoSospechoso;
                score.Reasons.Add($"Archivo sospechoso (+{_puntosArchivoSospechoso})");
            }

            // 6. ¿Está en un bloque de líneas sospechosas?
            if (allLines != null && allLines.Length > 1 && lineNumber > 0)
            {
                if (IsInSuspiciousBlock(lineNumber, allLines))
                {
                    score.Score += _puntosBloque;
                    score.Reasons.Add($"Bloque sospechoso (+{_puntosBloque})");
                }
            }

            // 7. ¿No es una palabra común?
            if (!_palabrasComunes.Contains(candidate.ToLowerInvariant()))
            {
                score.Score += _puntosNoPalabraComun;
                score.Reasons.Add($"No es palabra común (+{_puntosNoPalabraComun})");
            }

            // Determinar severidad
            if (score.Score >= _umbralAlert)
            {
                score.Severity = "High";
                score.RuleId = "risk-alto";
                score.Description = "Puntuación de riesgo ALTA";
            }
            else if (score.Score >= _umbralWarning)
            {
                score.Severity = "Medium";
                score.RuleId = "risk-medio";
                score.Description = "Puntuación de riesgo MEDIA";
            }
            else if (score.Score >= _umbralInfo)
            {
                score.Severity = "Low";
                score.RuleId = "risk-bajo";
                score.Description = "Puntuación de riesgo BAJA";
            }
            else
            {
                score.Severity = "Info";
                score.RuleId = "risk-info";
                score.Description = "Puntuación de riesgo INFO";
            }

            return score;
        }

        // ============================================================
        // EXTRAER TOKEN CANDIDATO A SECRETO DE LA LÍNEA
        // ============================================================
        public string ExtractCandidateSecret(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return string.Empty;

            // 1. Si tiene valor entre comillas: pass = "secreto"
            var quotedMatch = Regex.Match(line, @"[""']([^""'\r\n]{3,})[""']");
            if (quotedMatch.Success)
                return quotedMatch.Groups[1].Value.Trim();

            // 2. Si tiene etiqueta explícita con separadores: :, =, ->, =>, |
            var labelMatch = Regex.Match(line, @"(?i)\b(?:usuario|user(?:name)?|contraseña|password|pass|clave|pwd|u|c)\b\s*(?:[:=]|->|=>|\|)\s*([^\s""'#;,\(\)\[\]\{\}]+)");
            if (labelMatch.Success)
                return labelMatch.Groups[1].Value.Trim();

            // 3. De lo contrario, la línea completa recortada
            return line.Trim();
        }

        // ============================================================
        // VERIFICAR SI EL TOKEN ES UN FALSO POSITIVO TÍPICO DE CÓDIGO
        // ============================================================
        private bool EsFalsoPositivoToken(string token, string line)
        {
            if (string.IsNullOrWhiteSpace(token)) return true;

            // Placeholders: {keyword}, ${var}, <placeholder>, %param%
            if (token.StartsWith("{") && token.EndsWith("}")) return true;
            if (token.StartsWith("<") && token.EndsWith(">")) return true;
            if (token.StartsWith("$") && token.EndsWith("}")) return true;

            // Llamadas a función o métodos en código
            if (token.EndsWith("(") || token.Contains("(") || token.Contains(")")) return true;
            if (token.Contains(".") && (token.StartsWith("config.", StringComparison.OrdinalIgnoreCase) ||
                                       token.StartsWith("os.", StringComparison.OrdinalIgnoreCase) ||
                                       token.StartsWith("sys.", StringComparison.OrdinalIgnoreCase) ||
                                       token.StartsWith("self.", StringComparison.OrdinalIgnoreCase)))
                return true;

            // En la línea original, si inmediatamente sigue un '(' es una llamada
            if (Regex.IsMatch(line, Regex.Escape(token) + @"\s*\("))
                return true;

            // Palabras clave de programación no secretas
            var palabrasReservadas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "none", "null", "undefined", "true", "false", "empty", "default", "dummy", "test",
                "string", "integer", "boolean", "object", "array", "dict", "keyword", "value"
            };
            if (palabrasReservadas.Contains(token))
                return true;

            return false;
        }

        // ============================================================
        // MÉTODO: RECHAZAR LÍNEAS QUE NO SON CREDENCIALES
        // ============================================================
        private bool EsLineaNoCredencial(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return true;

            // 1. Solo símbolos (---, ===, ___, ***, etc.)
            if (Regex.IsMatch(line, @"^[-_=*#~\s]+$"))
                return true;

            // 2. Sin letras ni números
            if (!line.Any(char.IsLetterOrDigit))
                return true;

            // 3. Solo un carácter repetido (aaaa, 1111, ....)
            if (line.Distinct().Count() <= 2 && line.Length > 4)
                return true;

            // 4. Encabezados comunes
            if (Regex.IsMatch(line, @"^(?:#{1,6}\s|={2,}|-{2,}|\*{2,})"))
                return true;

            // 5. Etiquetas solas sin valor
            if (Regex.IsMatch(line, @"^(?:usuario|user|contraseña|password|pass|clave|pwd|username)\s*(?:[:=]|->|=>|\|)?\s*$",
                RegexOptions.IgnoreCase))
                return true;

            // 6. URLs
            if (Regex.IsMatch(line, @"^https?://", RegexOptions.IgnoreCase))
                return true;

            // 7. Placeholders únicos: {keyword}, ${variable}, <placeholder>
            if (Regex.IsMatch(line, @"^\s*[\{\$<].*[\}\>]\s*$"))
                return true;

            // 8. Instrucciones de código comunes que no contienen credenciales directas
            if (Regex.IsMatch(line, @"^\s*(?:import|from|def|class|return|using|namespace|public|private|protected|package)\b", RegexOptions.IgnoreCase))
                return true;

            // 9. Lectura de configuración / logs / llamadas a métodos
            if (Regex.IsMatch(line, @"\b(?:config\.get|os\.getenv|os\.environ|getenv|settings\.get|request\.|self\.|logger\.|print\(|console\.log)\b", RegexOptions.IgnoreCase))
                return true;

            // 10. Líneas de código que terminan en coma o paréntesis de apertura
            string trimmed = line.TrimEnd();
            if (trimmed.EndsWith("(") || trimmed.EndsWith(","))
                return true;

            return false;
        }

        // ============================================================
        // MÉTODO: VERIFICAR FORMATO DE CONTRASEÑA
        // ============================================================
        private bool HasPasswordFormat(string token)
        {
            if (string.IsNullOrEmpty(token) || token.Length < 8 || token.Length > 32)
                return false;

            // No debe contener espacios, paréntesis ni llamadas de función
            if (token.Contains(' ') || token.Contains('(') || token.Contains(')') || token.Contains('.'))
                return false;

            bool hasUpper = token.Any(char.IsUpper);
            bool hasLower = token.Any(char.IsLower);
            bool hasDigit = token.Any(char.IsDigit);
            return hasUpper && hasLower && hasDigit;
        }

        // ============================================================
        // MÉTODO: VERIFICAR SI ESTÁ EN UN BLOQUE SOSPECHOSO
        // ============================================================
        private bool IsInSuspiciousBlock(int lineNumber, string[] allLines)
        {
            int sospechosas = 0;
            int inicio = Math.Max(0, lineNumber - 4);
            int fin = Math.Min(allLines.Length, lineNumber + 3);

            for (int i = inicio; i < fin; i++)
            {
                if (i == lineNumber - 1) continue;

                var line = allLines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.Length < 3) continue;
                if (EsLineaNoCredencial(line)) continue;

                string cand = ExtractCandidateSecret(line);
                if (!string.IsNullOrEmpty(cand) && (HasPasswordFormat(cand) ||
                    Regex.IsMatch(line, @"(?i)\b(?:usuario|user(?:name)?|contraseña|password|pass|clave|pwd)\b\s*(?:[:=]|->|=>|\|)")))
                {
                    sospechosas++;
                }
            }

            return sospechosas >= 2;
        }
    }
}