using System.IO;
using System.Linq;
using System.Windows;
using NAudio.Wave;
using TommyVoice.Services;

namespace TommyVoice
{
    public partial class MainWindow : Window
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private string _audioFile = Path.Combine(Path.GetTempPath(), "tommy_input.wav");
        private bool _isRecording = false;

        private static readonly Dictionary<string, string> _env = LoadEnv(
            @"C:\Users\Jack Darius\Documents\IA\TOMMY\.env");

        private readonly string _systemPrompt;
        private readonly List<object> _conversationHistory = new List<object>();

        private readonly DeepgramService _deepgram;
        private readonly ClaudeService _claude;
        private readonly ElevenLabsService _elevenlabs;
        private readonly MemoryService _memory;

        public MainWindow()
        {
            InitializeComponent();

            var claudeMd = File.ReadAllText(@"C:\Users\Jack Darius\Documents\IA\TOMMY\CLAUDE.md");
            var memoryDir = @"C:\Users\Jack Darius\Documents\IA\TOMMY\memory\";
            var memoryFiles = Directory.GetFiles(memoryDir, "*.md");
            var memoryContent = string.Join("\n\n---\n\n", memoryFiles.Select(f => File.ReadAllText(f)));
            _systemPrompt = claudeMd + "\n\n---\n\n" + memoryContent;

            _deepgram = new DeepgramService(_env.GetValueOrDefault("DEEPGRAM_API_KEY", ""));
            _claude = new ClaudeService(
                _env.GetValueOrDefault("ANTHROPIC_API_KEY", ""),
                _env.GetValueOrDefault("CLAUDE_MODEL", "claude-sonnet-4-6"),
                int.Parse(_env.GetValueOrDefault("CLAUDE_MAX_TOKENS", "2048")));
            _elevenlabs = new ElevenLabsService(
                _env.GetValueOrDefault("ELEVENLABS_API_KEY", ""),
                _env.GetValueOrDefault("ELEVENLABS_VOICE_ID", ""));
            _memory = new MemoryService(
                _env.GetValueOrDefault("ANTHROPIC_API_KEY", ""),
                _env.GetValueOrDefault("CLAUDE_MODEL", "claude-sonnet-4-6"),
                @"C:\Users\Jack Darius\Documents\IA\TOMMY\memory\context.md",
                @"C:\Users\Jack Darius\Documents\IA\TOMMY\memory\session.json");
        }

        private static Dictionary<string, string> LoadEnv(string path)
        {
            return File.ReadAllLines(path)
                .Where(l => !l.StartsWith("#") && l.Contains("="))
                .ToDictionary(
                    l => l.Split('=')[0].Trim(),
                    l => l.Substring(l.IndexOf('=') + 1).Trim());
        }

        protected override async void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_conversationHistory.Count > 0)
            {
                e.Cancel = true;
                StatusLabel.Text = "Sauvegarde de la session...";
                await _memory.SummarizeAndUpdateContextAsync(_conversationHistory);
            }
            Application.Current.Shutdown();
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                _waveIn = new WaveInEvent();
                _waveIn.WaveFormat = new WaveFormat(16000, 1);
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
                _waveIn.StopRecording();
                _writer.Close();
                _waveIn.Dispose();
                _isRecording = false;
                RecordButton.Content = "🎙 Parler";
                StatusLabel.Text = "Traitement...";

                var transcription = await _deepgram.TranscribeAsync(_audioFile);
                _conversationHistory.Add(new { role = "user", content = transcription });
                _memory.SaveHistory(_conversationHistory);

                var response = await _claude.AskAsync(_systemPrompt, _conversationHistory);
                _conversationHistory.Add(new { role = "assistant", content = response });
                _memory.SaveHistory(_conversationHistory);

                ConversationBox.Text = "Toi : " + transcription + "\n\nTommy : " + response;
                StatusLabel.Text = "Prêt";

                await _elevenlabs.SpeakAsync(response);
            }
        }
    }
}
