using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;

namespace TommyVoice.Services
{
    public class ClaudeService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _model;
        private readonly int _maxTokens;

        public ClaudeService(string apiKey, string model, int maxTokens)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _model = model;
            _maxTokens = maxTokens;
        }

        public async Task<string> AskAsync(string systemPrompt, List<object> history)
        {
            try
            {
                var requestBody = new
                {
                    model = _model,
                    max_tokens = _maxTokens,
                    system = systemPrompt,
                    messages = history.ToArray()
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                var response = await _httpClient.PostAsync(
                    "https://api.anthropic.com/v1/messages", content);

                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(responseJson);
                return doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "";
            }
            catch (Exception ex)
            {
                return "Erreur Claude : " + ex.Message;
            }
        }

        public async IAsyncEnumerable<string> AskStreamingAsync(string systemPrompt, List<object> history)
        {
            var requestBody = new
            {
                model = _model,
                max_tokens = _maxTokens,
                system = systemPrompt,
                messages = history.ToArray(),
                stream = true
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            var response = await _httpClient.PostAsync(
                "https://api.anthropic.com/v1/messages", content);

            var stream = await response.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync();
                if (line == null || !line.StartsWith("data: ")) continue;

                var data = line.Substring(6);
                if (data == "[DONE]") break;

                string? delta = null;
                try
                {
                    var doc = JsonDocument.Parse(data);
                    var type = doc.RootElement.GetProperty("type").GetString();
                    if (type == "content_block_delta")
                        delta = doc.RootElement.GetProperty("delta").GetProperty("text").GetString();
                }
                catch { }

                if (delta != null)
                    yield return delta;
            }
        }
    }
}
