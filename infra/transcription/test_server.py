import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

SPEC = importlib.util.spec_from_file_location('transcription_server', Path(__file__).with_name('server.py'))
server = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(server)


class TranscriptionTests(unittest.TestCase):
    def test_decodes_audio_before_transcribing_and_returns_saved_output(self):
        original = subprocess.run
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            recording = directory / 'fixture.webm'
            subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-f', 'lavfi', '-i', 'sine=frequency=440:duration=0.2',
                            '-c:a', 'libopus', str(recording)], check=True)

            def invoke(args, **kwargs):
                if args[0] == 'whisper-cli':
                    output = Path(args[args.index('-of') + 1]).with_suffix('.txt')
                    output.write_text(' Synthetic transcript.\n')
                    return subprocess.CompletedProcess(args, 0)
                return original(args, **kwargs)

            with patch.object(server.subprocess, 'run', side_effect=invoke):
                self.assertEqual('Synthetic transcript.', server.transcribe(recording.read_bytes(), directory))

    def test_rejects_non_audio_bytes_before_model_is_called(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaises(subprocess.CalledProcessError):
                server.transcribe(b'<html>not audio</html>', Path(temporary))

    def test_rejects_overlong_decoded_audio(self):
        original = subprocess.run
        with tempfile.TemporaryDirectory() as temporary:
            directory = Path(temporary)
            recording = directory / 'long.ogg'
            original(['ffmpeg', '-y', '-loglevel', 'error', '-f', 'lavfi', '-i', 'sine=frequency=440:duration=121',
                      '-c:a', 'libopus', str(recording)], check=True)
            with self.assertRaises(ValueError):
                server.transcribe(recording.read_bytes(), directory)


if __name__ == '__main__':
    unittest.main()
