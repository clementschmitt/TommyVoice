using NAudio.Wave;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

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

        public async Task SpeakStreamingAsync(IAsyncEnumerable<string> textStream)
        {
            var wsUrl = $"wss://api.elevenlabs.io/v1/text-to-speech/{_voiceId}/stream-input?model_id=eleven_multilingual_v2&output_format=pcm_16000";

            using var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("xi-api-key", _apiKey);
            await ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);

            var initMsg = JsonSerializer.Serialize(new
            {
                text = " ",
                voice_settings = new { stability = 0.5, similarity_boost = 0.8 },
                xi_api_key = _apiKey
            });
            await ws.SendAsync(Encoding.UTF8.GetBytes(initMsg), WebSocketMessageType.Text, true, CancellationToken.None);

            var waveFormat = new WaveFormat(16000, 16, 1);
            var bufferedProvider = new BufferedWaveProvider(waveFormat)
            {
                BufferDuration = TimeSpan.FromMinutes(10),
                DiscardOnBufferOverflow = true
            };
            using var waveOut = new WaveOutEvent();
            waveOut.Init(bufferedProvider);

            var readyToPlay = new TaskCompletionSource<bool>();

            var sendTask = Task.Run(async () =>
            {
                try
                {
                    var buffer = new StringBuilder();
                    await foreach (var chunk in textStream)
                    {
                        buffer.Append(chunk);
                        var text = buffer.ToString();
                        var boundary = text.LastIndexOfAny(new[] { '.', ',', '!', '?', '\n' });
                        if (boundary >= 0)
                        {
                            var toSend = text[..(boundary + 1)];
                            var msg = JsonSerializer.Serialize(new { text = toSend });
                            await ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, CancellationToken.None);
                            buffer.Clear();
                            buffer.Append(text[(boundary + 1)..]);
                        }
                    }
                    if (buffer.Length > 0)
                    {
                        var msg = JsonSerializer.Serialize(new { text = buffer.ToString() });
                        await ws.SendAsync(Encoding.UTF8.GetBytes(msg), WebSocketMessageType.Text, true, CancellationToken.None);
                    }
                    var eosMsg = JsonSerializer.Serialize(new { text = "" });
                    await ws.SendAsync(Encoding.UTF8.GetBytes(eosMsg), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch { }
            });

            var receiveBuffer = new byte[8192];
            var receiveTask = Task.Run(async () =>
            {
                while (ws.State == WebSocketState.Open)
                {
                    try
                    {
                        var ms = new MemoryStream();
                        WebSocketReceiveResult result;
                        do
                        {
                            result = await ws.ReceiveAsync(receiveBuffer, CancellationToken.None);
                            ms.Write(receiveBuffer, 0, result.Count);
                        } while (!result.EndOfMessage);

                        if (result.MessageType == WebSocketMessageType.Close) break;

                        if (result.MessageType == WebSocketMessageType.Binary)
                        {
                            var audioBytes = ms.ToArray();
                            if (audioBytes.Length > 0)
                            {
                                bufferedProvider.AddSamples(audioBytes, 0, audioBytes.Length);
                                if (bufferedProvider.BufferedBytes > 16000)
                                    readyToPlay.TrySetResult(true);
                            }
                        }
                        else if (result.MessageType == WebSocketMessageType.Text)
                        {
                            var json = Encoding.UTF8.GetString(ms.ToArray());
                            try
                            {
                                var doc = JsonDocument.Parse(json);
                                if (doc.RootElement.TryGetProperty("audio", out var audioEl))
                                {
                                    var b64 = audioEl.GetString() ?? "";
                                    if (b64.Length > 0)
                                    {
                                        var audioBytes = Convert.FromBase64String(b64);
                                        if (audioBytes.Length > 0)
                                        {
                                            bufferedProvider.AddSamples(audioBytes, 0, audioBytes.Length);
                                            if (bufferedProvider.BufferedBytes > 16000)
                                                readyToPlay.TrySetResult(true);
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch { break; }
                }
                readyToPlay.TrySetResult(true);
            });

            await readyToPlay.Task;

            if (bufferedProvider.BufferedBytes > 0)
                waveOut.Play();

            await Task.WhenAll(sendTask, receiveTask);

            while (waveOut.PlaybackState == PlaybackState.Playing)
                await Task.Delay(200);
        }
    }
}
