using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

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
    }
}
