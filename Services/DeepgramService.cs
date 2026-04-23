using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace TommyVoice.Services
{
    public class DeepgramService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public DeepgramService(string apiKey)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
        }

        public async Task<string> TranscribeAsync(string audioPath)
        {
            try
            {
                var audioBytes = File.ReadAllBytes(audioPath);
                var content = new ByteArrayContent(audioBytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.deepgram.com/v1/listen?model=nova-2&language=fr");
                request.Headers.Authorization = new AuthenticationHeaderValue("Token", _apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                return doc.RootElement
                    .GetProperty("results")
                    .GetProperty("channels")[0]
                    .GetProperty("alternatives")[0]
                    .GetProperty("transcript")
                    .GetString() ?? "";
            }
            catch (Exception ex)
            {
                return "Erreur transcription : " + ex.Message;
            }
        }
    }
}
