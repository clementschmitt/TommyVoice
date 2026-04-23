using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using NAudio.Wave;

namespace TommyVoice.Services
{
    public class ElevenLabsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _voiceId;

        public ElevenLabsService(string apiKey, string voiceId)
        {
            _httpClient = new HttpClient();
            _apiKey = apiKey;
            _voiceId = voiceId;
        }

        public async Task SpeakAsync(string text)
        {
            try
            {
                var body = JsonSerializer.Serialize(new
                {
                    text = text,
                    model_id = "eleven_multilingual_v2"
                });
                var content = new StringContent(body, Encoding.UTF8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api.elevenlabs.io/v1/text-to-speech/{_voiceId}");
                request.Headers.Add("xi-api-key", _apiKey);
                request.Content = content;

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                    return;

                var audioBytes = await response.Content.ReadAsByteArrayAsync();
                var tempFile = Path.Combine(Path.GetTempPath(), "tommy_output.mp3");
                await File.WriteAllBytesAsync(tempFile, audioBytes);

                using var reader = new Mp3FileReader(tempFile);
                using var waveOut = new WaveOutEvent();
                waveOut.Init(reader);
                waveOut.Play();
                while (waveOut.PlaybackState == PlaybackState.Playing)
                    await Task.Delay(100);
            }
            catch { }
        }
    }
}
