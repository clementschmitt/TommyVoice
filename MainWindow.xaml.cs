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

        private readonly string _anthropicKey = _env.GetValueOrDefault("ANTHROPIC_API_KEY", "");
        private readonly string _claudeModel = _env.GetValueOrDefault("CLAUDE_MODEL", "claude-sonnet-4-6");
        private readonly int _claudeMaxTokens = int.Parse(_env.GetValueOrDefault("CLAUDE_MAX_TOKENS", "2048"));
        private readonly string _deepgramKey = _env.GetValueOrDefault("DEEPGRAM_API_KEY", "");

        private readonly string _systemPrompt;

        private List<object> _conversationHistory = new List<object>();


        public MainWindow()
        {
            InitializeComponent();
            var claudeMd = System.IO.File.ReadAllText(@"C:\Users\Jack Darius\Documents\IA\TOMMY\CLAUDE.md");
            var identityMd = System.IO.File.ReadAllText(@"C:\Users\Jack Darius\Documents\IA\TOMMY\memory\identity.md");
            _systemPrompt = claudeMd + "\n\n---\n\n" + identityMd;
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
                var audioBytes = File.ReadAllBytes(audioPath);
                var content = new ByteArrayContent(audioBytes);
                content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

                var request = new HttpRequestMessage(HttpMethod.Post,
                    "https://api.deepgram.com/v1/listen?model=nova-2&language=fr");
                request.Headers.Authorization = new AuthenticationHeaderValue("Token", _deepgramKey);
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

        private async Task<string> AskClaude(string userMessage)
        {
            try
            {
                // 1. Ajouter le message utilisateur à l'historique
                _conversationHistory.Add(new { role = "user", content = userMessage });

                // 2. Construire la requête avec tout l'historique
                var requestBody = new
                {
                    model = _claudeModel,
                    max_tokens = _claudeMaxTokens,
                    system = _systemPrompt,
                    messages = _conversationHistory.ToArray()
                };

                // 3. ... envoyer la requête ...
                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _anthropicKey);
                _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                _httpClient.DefaultRequestHeaders.Accept.Add(
                    new MediaTypeWithQualityHeaderValue("application/json"));

                // 4. Récupérer la réponse
                var response = await _httpClient.PostAsync(
                    "https://api.anthropic.com/v1/messages", content);

                var responseJson = await response.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(responseJson);
                var reponse = doc.RootElement
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString() ?? "";

                // 5. Ajouter la réponse de Tommy à l'historique
                _conversationHistory.Add(new { role = "assistant", content = reponse });

                return reponse;

            } 
            catch (Exception ex) 
            {
                return "Erreur : " + ex.Message;
            }
        }
    }
}