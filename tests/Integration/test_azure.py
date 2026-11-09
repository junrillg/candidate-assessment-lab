"""Account-offline regression checks for the Azure bootstrap's data-loss boundary."""
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import call, patch

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'scripts'))
import lab


# Real temporary file bytes exercise the whole-device zero/read check. Destructive
# commands are replaced before calling the sourced function; main never executes.
DISK_HARNESS = r'''
source "$AZURE_TEST_SCRIPT"
lsblk() {
  if test "$2" = TYPE; then
    if test "$AZURE_TEST_MODE" = partition; then printf 'disk\npart\n'; else echo disk; fi
  elif [[ "$AZURE_TEST_MODE" = mounted* ]]; then echo "$AZURE_TEST_TARGET"
  elif test "$AZURE_TEST_MODE" = inuse; then echo /another/mount
  fi
}
mountpoint() { [[ "$AZURE_TEST_MODE" = mounted* ]]; }
findmnt() { echo "$AZURE_TEST_DEVICE"; }
wipefs() {
  case "$AZURE_TEST_MODE" in
    wipeerror) return 1;;
    existing|mounted|mounted-conflict) echo ext4;;
    wrongfs) echo xfs;;
    signature) echo crypto_LUKS;;
  esac
}
blkid() {
  if test "$1" != --probe; then echo test-uuid; return; fi
  case "$AZURE_TEST_MODE" in
    existing|mounted|mounted-conflict) echo ext4;;
    wrongfs) echo xfs;;
    probeerror) return 4;;
    *) return 2;;
  esac
}
blockdev() { echo 4; }
cmp() {
  if test "$AZURE_TEST_MODE" = readerror; then return 2; fi
  command cmp "$@"
}
mkfs.ext4() { echo format >>"$AZURE_TEST_LOG"; }
mount() { echo mount >>"$AZURE_TEST_LOG"; }
prepare_data_disk "$AZURE_TEST_DEVICE" "$AZURE_TEST_TARGET" "$AZURE_TEST_FSTAB"
'''


class AzureBootstrapTests(unittest.TestCase):
    def run_disk(self, mode):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            device, target, fstab, log = (root / name for name in ('disk', 'data', 'fstab', 'actions'))
            device.write_bytes(b'live' if mode == 'nonzero' else b'\0' * 4)
            target.mkdir()
            fstab.write_text(f'UUID=other {target} ext4 defaults 0 2\n' if mode == 'mounted-conflict' else '')
            log.touch()
            result = subprocess.run(['bash', '-c', DISK_HARNESS], capture_output=True, text=True, env={
                **os.environ,
                'AZURE_TEST_SCRIPT': str(ROOT / 'infra/azure/bootstrap.sh'),
                'AZURE_TEST_MODE': mode,
                'AZURE_TEST_DEVICE': str(device),
                'AZURE_TEST_TARGET': str(target),
                'AZURE_TEST_FSTAB': str(fstab),
                'AZURE_TEST_LOG': str(log),
            })
            return result, log.read_text().splitlines(), fstab.read_text()

    def test_verified_blank_disk_is_initialized_and_persistently_mounted(self):
        result, actions, fstab = self.run_disk('blank')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(['format', 'mount'], actions)
        self.assertIn('UUID=test-uuid ', fstab)

    def test_existing_ext4_disk_is_reused_without_format(self):
        result, actions, fstab = self.run_disk('existing')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual(['mount'], actions)
        self.assertIn('UUID=test-uuid ', fstab)

    def test_probe_read_signature_and_usage_failures_never_format(self):
        for mode in ('probeerror', 'wipeerror', 'readerror', 'nonzero', 'wrongfs',
                     'signature', 'partition', 'inuse', 'mounted-empty'):
            with self.subTest(mode=mode):
                result, actions, fstab = self.run_disk(mode)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual([], actions)
                self.assertEqual('', fstab)

    def test_matching_mount_still_persists_fstab_without_remounting(self):
        result, actions, fstab = self.run_disk('mounted')
        self.assertEqual(0, result.returncode, result.stderr)
        self.assertEqual([], actions)
        self.assertIn('UUID=test-uuid ', fstab)

    def test_conflicting_persistent_mount_is_rejected(self):
        result, actions, fstab = self.run_disk('mounted-conflict')
        self.assertNotEqual(0, result.returncode)
        self.assertEqual([], actions)
        self.assertNotIn('UUID=test-uuid', fstab)

    def test_bootstrap_and_cloud_init_are_valid_bash(self):
        for filename in ('bootstrap.sh', 'cloud-init.sh.tftpl'):
            with self.subTest(filename=filename):
                subprocess.run(['bash', '-n', ROOT / 'infra/azure' / filename], check=True)


class AzureCommandTests(unittest.TestCase):
    def test_validation_runs_only_offline_provider_checks(self):
        with patch.object(sys, 'argv', ['lab.py', 'azure-validate']), patch.object(lab, 'terraform') as terraform:
            lab.main()
        self.assertEqual([
            call('fmt', '-check', profile='azure'), call('validate', profile='azure'),
            call('test', profile='azure'),
        ], terraform.call_args_list)

    def test_plan_and_apply_use_the_azure_saved_plan(self):
        with tempfile.TemporaryDirectory() as directory, patch.object(lab, 'LOCAL', Path(directory)):
            for command, expected in (
                ('azure-plan', call('plan', '-input=false', '-out=../../.local/azure.tfplan', profile='azure')),
                ('azure-apply', call('apply', '../../.local/azure.tfplan', profile='azure')),
            ):
                with self.subTest(command=command), patch.object(sys, 'argv', ['lab.py', command]), patch.object(lab, 'terraform') as terraform:
                    lab.main()
                    self.assertEqual([expected], terraform.call_args_list)


if __name__ == '__main__':
    unittest.main()
