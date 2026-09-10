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
    public class SentinelApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _computerName;
        private readonly string _userName;
        private readonly string _ipAddress;

        public SentinelApiClient(string baseUrl, string computerName, string userName)
        {
            _baseUrl = string.IsNullOrEmpty(baseUrl) ? "http://localhost:3000/api/v1" : baseUrl.TrimEnd('/');
            _computerName = computerName;
            _userName = userName;
            _ipAddress = GetLocalIPAddress();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        }

        public async Task SendHeartbeatAsync()
        {
            try
            {
                string url = $"{_baseUrl}/agents/heartbeat";

                var payload = new
                {
                    hostname = _computerName,
                    username = _userName,
                    ipAddress = _ipAddress,
                    version = "1.0.0",
                    status = "active"
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error enviando Heartbeat a Node.js API: {ex.Message}", _computerName, _userName);
            }
        }

        public async Task SendFindingAsync(CredentialFinding finding)
        {
            try
            {
                string url = $"{_baseUrl}/agents/findings";

                var payload = new
                {
                    agentId = _computerName,
                    hostname = _computerName,
                    username = _userName,
                    filePath = finding.FilePath,
                    ruleId = finding.RuleId,
                    description = finding.Description ?? "Credencial detectada",
                    severity = finding.Severity,
                    secret = finding.Secret,
                    lineNumber = finding.LineNumber,
                    fullMatch = finding.FullMatch ?? finding.Secret
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error enviando Hallazgo a Node.js API: {ex.Message}", _computerName, _userName);
            }
        }

        public async Task SendEventAsync(string eventType, string severity, string description)
        {
            try
            {
                string url = $"{_baseUrl}/agents/events";

                var payload = new
                {
                    agentId = _computerName,
                    hostname = _computerName,
                    eventType = eventType,
                    severity = severity,
                    description = description
                };

                string json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                await _httpClient.PostAsync(url, content);
            }
            catch (Exception ex)
            {
                Logger.Error($"Error enviando Evento a Node.js API: {ex.Message}", _computerName, _userName);
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
