using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TommyVoice.Services
{
    public class MemoryService
    {
        private readonly string _contextPath;
        private readonly string _sessionPath;
        private readonly HttpClient _httpClient;
        private readonly string _claudeApiKey;
        private readonly string _claudeModel;

        public MemoryService(string claudeApiKey, string claudeModel, string contextPath, string sessionPath)
        {
            _httpClient = new HttpClient();
            _claudeApiKey = claudeApiKey;
            _claudeModel = claudeModel;
            _contextPath = contextPath;
            _sessionPath = sessionPath;
        }

        public void SaveHistory(List<object> history)
        {
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_sessionPath, json);
        }

        public async Task SummarizeAndUpdateContextAsync(List<object> history)
        {
            try
            {
                var historyJson = JsonSerializer.Serialize(history);
                var prompt = $"Voici la conversation de cette session :\n{historyJson}\n\nRésume en quelques points les informations importantes à retenir pour le contexte futur. Format Markdown concis.";

                var requestBody = new
                {
                    model = _claudeModel,
                    max_tokens = 500,
                    messages = new[] { new { role = "user", content = prompt } }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _claudeApiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.PostAsync("https://api.anthropic.com/v1/messages", content);
                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(responseJson);
                var summary = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString() ?? "";

                var existing = File.ReadAllText(_contextPath);
                var updated = existing + $"\n\n## Session du {DateTime.Now:dd/MM/yyyy HH:mm}\n{summary}";
                File.WriteAllText(_contextPath, updated);
            }
            catch { }
        }
    }
}
