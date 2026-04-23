"""
Whisper transcription script — called by TommyVoice C# app.
Usage: python whisper_transcribe.py <audio_file_path>
Output: transcribed text on stdout
"""

import sys
import whisper

def transcribe(audio_path: str) -> str:
    model = whisper.load_model("base")
    result = model.transcribe(audio_path, language="fr")
    return result["text"].strip()

if __name__ == "__main__":
    if len(sys.argv) < 2:
        print("Usage: python whisper_transcribe.py <audio_file>", file=sys.stderr)
        sys.exit(1)
    print(transcribe(sys.argv[1]))
