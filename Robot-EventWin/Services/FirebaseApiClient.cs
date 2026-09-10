using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CredentialScanner.Models;
using CredentialScanner.Utils;

namespace CredentialScanner.Services
{
    public class FirebaseApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _projectId;
        private readonly string _baseUrl;
        private readonly string _computerName;
        private readonly string _userName;
        private readonly string _ipAddress;

        public FirebaseApiClient(string projectId, string computerName, string userName)
        {
            _projectId = string.IsNullOrEmpty(projectId) ? "miatech-sentinel" : projectId;
            _computerName = computerName;
            _userName = userName;
            _ipAddress = GetLocalIPAddress();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            _baseUrl = $"https://firestore.googleapis.com/v1/projects/{_projectId}/databases/(default)/documents";
        }

        public async Task SendHeartbeatAsync()
        {
            try
            {
                string url = $"{_baseUrl}/agents/{_computerName}";

                var payload = new
                {
                    fields = new Dictionary<string, object>
                    {
                        { "hostname", new { stringValue = _computerName } },
                        { "username", new { stringValue = _userName } },
                        { "ipAddress", new { stringValue = _ipAddress } },
                        { "version", new { stringValue = "1.0.0" } },
                        { "status", new { stringValue = "active" } },
                        { "lastPing", new { timestampValue = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") } }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Try PATCH first to update document, fallback to POST
                var response = await _httpClient.PatchAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    string createUrl = $"{_baseUrl}/agents?documentId={_computerName}";
                    await _httpClient.PostAsync(createUrl, new StringContent(json, Encoding.UTF8, "application/json"));
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Error enviando Heartbeat a Firebase: {ex.Message}", _computerName, _userName);
            }
        }

        public async Task SendFindingAsync(CredentialFinding finding)
        {
            try
            {
                string url = $"{_baseUrl}/findings";

                var payload = new
                {
                    fields = new Dictionary<string, object>
                    {
                        { "agentId", new { stringValue = _computerName } },
                        { "hostname", new { stringValue = _computerName } },
                        { "username", new { stringValue = _userName } },
                        { "filePath", new { stringValue = finding.FilePath } },
                        { "ruleId", new { stringValue = finding.RuleId } },
                        { "description", new { stringValue = finding.Description ?? "Credencial detectada" } },
                        { "severity", new { stringValue = finding.Severity } },
                        { "secret", new { stringValue = finding.Secret } },
                        { "lineNumber", new { integerValue = finding.LineNumber } },
                        { "fullMatch", new { stringValue = finding.FullMatch ?? finding.Secret } },
                        { "detectedAt", new { timestampValue = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") } }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error enviando Hallazgo a Firebase: {ex.Message}", _computerName, _userName);
            }
        }

        public async Task SendEventAsync(string eventType, string severity, string description)
        {
            try
            {
                string url = $"{_baseUrl}/events";

                var payload = new
                {
                    fields = new Dictionary<string, object>
                    {
                        { "agentId", new { stringValue = _computerName } },
                        { "hostname", new { stringValue = _computerName } },
                        { "eventType", new { stringValue = eventType } },
                        { "severity", new { stringValue = severity } },
                        { "description", new { stringValue = description } },
                        { "createdAt", new { timestampValue = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") } }
                    }
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error enviando Evento a Firebase: {ex.Message}", _computerName, _userName);
            }
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = System.Net.Dns.GetHostEntry(System.Net.Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }
    }
}
