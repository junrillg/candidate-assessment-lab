from contextlib import redirect_stdout
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts'))
import lab
import backup


class OperationsTests(unittest.TestCase):
    def test_configuration_is_private_and_repeatable_without_printing_passwords(self):
        with tempfile.TemporaryDirectory() as directory:
            local = Path(directory)
            output = io.StringIO()
            with patch.object(lab, 'LOCAL', local), patch.object(lab, 'ENV', local / 'lab.env'), redirect_stdout(output):
                lab.configure()
                before = (local / 'credentials.json').read_bytes()
                lab.configure()
                self.assertEqual(before, (local / 'credentials.json').read_bytes())
            payload = json.loads(before)
            self.assertEqual(3, len(payload['users']))
            self.assertEqual(3, len({u['password'] for u in payload['users']}))
            for user in payload['users']:
                self.assertNotIn(user['password'], output.getvalue())
                self.assertGreaterEqual(len(user['password']), 20)
            for path in [local / 'credentials.json', local / 'lab.env']:
                self.assertEqual(0, path.stat().st_mode & 0o077)

    def test_corrupt_backup_is_rejected_before_touching_runtime(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / 'manifest.json').write_text(json.dumps({name: 'wrong' for name in backup.FILES}))
            for name in backup.FILES:
                (path / name).write_bytes(b'corrupted')
            with patch.object(backup, 'compose') as runtime:
                with self.assertRaisesRegex(RuntimeError, 'checksum'):
                    backup.restore(path)
                runtime.assert_not_called()


if __name__ == '__main__':
    unittest.main()
