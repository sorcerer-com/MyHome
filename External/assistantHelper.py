import sys
import json
import numpy as np
import base64
from faster_whisper import WhisperModel
import traceback

# tiny, tiny.en, base, base.en, small, small.en, distil-small.en, medium, medium.en, distil-medium.en, 
# large-v1, large-v2, large-v3, large, distil-large-v2 or distil-large-v3
# https://github.com/rhasspy/models/releases
WHISPER_MODEL_SIZE = "./models/small-int8/" 
WHISPER_LANGUAGE = "bg"

whisper = None

def transcribe(data):
    global whisper
    audio_data = np.frombuffer(audio, np.int16).astype(np.float32) / 255.0

    if whisper is None:
        whisper = WhisperModel(WHISPER_MODEL_SIZE, device="cpu",
                                compute_type="int8", download_root="./models")

    segments, _ = whisper.transcribe(audio_data,
                                     language=WHISPER_LANGUAGE,
                                     beam_size=2,
                                     vad_filter=True,
                                     vad_parameters=dict(min_silence_duration_ms=500))

    return [(segment.start, segment.end, segment.text) for segment in segments]


if __name__ == "__main__":
    try:
        for line in sys.stdin:
            try:
                if line.strip() == "transcribe":
                    audio = base64.b64decode(sys.stdin.readline())
                    res = json.dumps(transcribe(audio), ensure_ascii=False)
                    print(base64.b64encode(res.encode('utf-8')))

                if line.strip() == "exit":
                    break

                sys.stdout.flush()
            except Exception:
                sys.stderr.write(
                    f"Failed to {line}:\n{traceback.format_exc()}\n")
    finally:
        sys.stderr.write("Stop assistant helper\n")
