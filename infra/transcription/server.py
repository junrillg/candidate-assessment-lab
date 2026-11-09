"""Bounded private HTTP adapter for whisper.cpp. No request bodies enter logs."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import threading
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

MAX_BYTES = 16 * 1024 * 1024
MODEL_HASH = '60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe'
MODEL_URL = 'https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin'
MODEL = Path(os.environ.get('WHISPER_MODEL', '/models/ggml-base.bin'))
SLOT = threading.BoundedSemaphore(1)


def ensure_model():
    MODEL.parent.mkdir(parents=True, exist_ok=True)
    if not MODEL.exists():
        target = MODEL.with_suffix('.partial')
        try:
            with urllib.request.urlopen(MODEL_URL, timeout=120) as response, target.open('wb') as output:
                shutil.copyfileobj(response, output)
            if hashlib.sha256(target.read_bytes()).hexdigest() != MODEL_HASH:
                raise RuntimeError('Whisper model checksum failed')
            target.replace(MODEL)
        finally:
            target.unlink(missing_ok=True)
    if hashlib.sha256(MODEL.read_bytes()).hexdigest() != MODEL_HASH:
        raise RuntimeError('Whisper model checksum failed')


def transcribe(data, directory):
    source, wav, output = directory / 'source', directory / 'audio.wav', directory / 'transcript'
    source.write_bytes(data)
    # MediaRecorder WebM may omit duration; measure the bounded decoded WAV instead.
    subprocess.run(['ffmpeg', '-nostdin', '-y', '-loglevel', 'error', '-protocol_whitelist', 'file,pipe',
                    '-format_whitelist', 'matroska,webm,ogg,mov,mp4,m4a,3gp,3g2,mj2,wav', '-i', str(source), '-vn',
                    '-t', '121', '-ac', '1', '-ar', '16000', '-c:a', 'pcm_s16le', str(wav)],
                   capture_output=True, timeout=30, check=True)
    probe = subprocess.run(['ffprobe', '-v', 'error', '-show_entries', 'format=duration', '-of', 'json', str(wav)],
                           capture_output=True, timeout=15, check=True)
    duration = float(json.loads(probe.stdout)['format']['duration'])
    if not 0 < duration <= 120.5:
        raise ValueError('Recording must be at most two minutes')
    subprocess.run(['whisper-cli', '-m', str(MODEL), '-f', str(wav), '-l', 'auto', '-np', '-nt', '-otxt', '-of', str(output),
                    '-t', os.environ.get('WHISPER_THREADS', '2')], capture_output=True, timeout=150, check=True)
    text = output.with_suffix('.txt').read_text().strip()
    if not text or len(text) > 20000:
        raise ValueError('No usable transcript')
    return text


class Handler(BaseHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def reply(self, status, payload):
        body = json.dumps(payload).encode()
        self.send_response(status)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        self.reply(200 if self.path == '/health' and MODEL.is_file() else 404, {'status': 'ready' if MODEL.is_file() else 'starting'})

    def do_POST(self):
        if self.path != '/transcribe':
            return self.reply(404, {'error': 'Not found'})
        if self.headers.get('Content-Type', '').split(';', 1)[0] not in {'audio/webm', 'audio/ogg', 'audio/mp4', 'audio/wav'}:
            return self.reply(415, {'error': 'Unsupported audio format'})
        try:
            size = int(self.headers.get('Content-Length', '0'))
        except ValueError:
            return self.reply(400, {'error': 'Invalid content length'})
        if not 0 < size <= MAX_BYTES or self.headers.get('Transfer-Encoding'):
            return self.reply(413, {'error': 'Audio must be a whole file of at most 16 MiB'})
        if not SLOT.acquire(blocking=False):
            return self.reply(503, {'error': 'Transcriber is busy; retry later'})
        try:
            self.connection.settimeout(30)
            data = self.rfile.read(size)
            if len(data) != size:
                return self.reply(400, {'error': 'Incomplete audio'})
            with tempfile.TemporaryDirectory(prefix='assessment-audio-') as directory:
                text = transcribe(data, Path(directory))
            self.reply(200, {'text': text})
        except (ValueError, KeyError, subprocess.CalledProcessError):
            self.reply(422, {'error': 'Audio could not be transcribed'})
        except (TimeoutError, subprocess.TimeoutExpired):
            self.reply(504, {'error': 'Transcription timed out'})
        except Exception:
            self.reply(503, {'error': 'Transcription is temporarily unavailable'})
        finally:
            SLOT.release()


if __name__ == '__main__':
    ensure_model()
    ThreadingHTTPServer(('0.0.0.0', 8081), Handler).serve_forever()
