using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using NAudio.Wave;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;


namespace TommyVoice
{
    public partial class MainWindow : Window
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string _audioFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tommy_input.wav");
        private bool _isRecording = false;

        private readonly HttpClient _httpClient = new HttpClient();

        private static readonly Dictionary<string, string> _env = LoadEnv(
        @"C:\Users\Jack Darius\Documents\IA\TOMMY\.env");

        private readonly string _openAiKey = _env.GetValueOrDefault("OPENAI_API_KEY", "");
        private readonly string _anthropicKey = _env.GetValueOrDefault("ANTHROPIC_API_KEY", "");
        private readonly string _claudeModel = _env.GetValueOrDefault("CLAUDE_MODEL", "claude-sonnet-4-6");
        private readonly int _claudeMaxTokens = int.Parse(_env.GetValueOrDefault("CLAUDE_MAX_TOKENS", "2048"));
        private readonly string _systemPrompt = System.IO.File.ReadAllText(@"C:\Users\Jack Darius\Documents\IA\TOMMY\memory\identity.md");


        public MainWindow()
        {
            InitializeComponent();
        }

        private static Dictionary<string, string> LoadEnv(string path)
        {
            return System.IO.File.ReadAllLines(path)
                .Where(l => !l.StartsWith("#") && l.Contains("="))
                .ToDictionary(
                    l => l.Split('=')[0].Trim(),
                    l => l.Substring(l.IndexOf('=') + 1).Trim()
                );
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                //Démarrer l'enregistrement
                _waveIn = new WaveInEvent();
                _waveIn.WaveFormat = new WaveFormat(16000, 1); // 16 kHz, mono
                _writer = new WaveFileWriter(_audioFile, _waveIn.WaveFormat);

                _waveIn.DataAvailable += (s, a) =>
                {
                    _writer.Write(a.Buffer, 0, a.BytesRecorded);
                };

                _waveIn.StartRecording();
                _isRecording = true;
                RecordButton.Content = "⏹ Arrêter";
                StatusLabel.Text = "Enregistrement...";
            }
            else
            {
                //Arrêter l'enregistrement
                _waveIn.StopRecording();
                _writer.Close();
                _waveIn.Dispose();
                _isRecording = false;
                RecordButton.Content = "🎙 Parler";
                StatusLabel.Text = "Traitement...";
                var transcription = await TranscribeAudio(_audioFile);
                ConversationBox.Text = "Toi : " + transcription;
                var response = await AskClaude(transcription);
                ConversationBox.Text = "Toi : " + transcription + "\n\nTommy : " + response;
                StatusLabel.Text = "Transcription terminée";
            }
        }
        private async Task<string> TranscribeAudio(string audioPath)
        {
            try
            {
                using var form = new MultipartFormDataContent();
                using var fileStream = File.OpenRead(audioPath);
                using var fileContent = new StreamContent(fileStream);

                fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                form.Add(fileContent, "file", "audio.wav");
                form.Add(new StringContent("whisper-1"), "model");
                form.Add(new StringContent("fr"), "language");

                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _openAiKey);

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/audio/transcriptions", form);

                var json = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);
                return doc.RootElement.GetProperty("text").GetString() ?? "";
            }
            catch (Exception ex)
            {
                return "";
            }
        }
        private async Task<string> AskClaude(string userMessage)
        {
            try
            {
                var requestBody = new
                {
                    model = "claude-sonnet-4-6",
                    max_tokens = _claudeMaxTokens,
                    system = _systemPrompt,
                    messages = new[]
                    {
                        new { role = "user", content = userMessage }
                    }
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _anthropicKey);
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
                return "Erreur : " + ex.Message;
            }
        }
    }
}