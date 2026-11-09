#!/usr/bin/env python3
"""Private, quiesced backups; restore refuses any existing application tables."""
import hashlib
import json
import os
from pathlib import Path
import shutil
import sys
import tarfile
from datetime import datetime, timezone
from lab import LOCAL, compose

FILES = ['database.sql', 'recordings-and-keys.tar.gz', 'credentials.json']


def digest(path):
    with path.open('rb') as stream:
        checksum = hashlib.sha256()
        while chunk := stream.read(1024 * 1024):
            checksum.update(chunk)
        return checksum.hexdigest()


def capture(path, *args):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, 'wb') as output:
        compose(*args, stdout=output)


def backup():
    destination = LOCAL / 'backups' / datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    destination.mkdir(parents=True, mode=0o700)
    running = json.loads(compose('ps', '--format', 'json', 'app', capture_output=True, text=True).stdout or '{}')
    was_running = bool(running) and (running[0] if isinstance(running, list) else running).get('State') == 'running'
    if was_running:
        compose('stop', 'app')
    try:
        capture(destination / 'database.sql', 'exec', '-T', 'postgres', 'pg_dump', '-U', 'assessment', '-d', 'assessment',
                '--no-owner', '--no-privileges')
        capture(destination / 'recordings-and-keys.tar.gz', 'run', '--rm', '--no-deps', '-T', '--user', '0:0',
                '--entrypoint', 'tar', 'app', '-C', '/var/lib/assessment', '-czf', '-', 'recordings', 'keys')
        shutil.copyfile(LOCAL / 'credentials.json', destination / 'credentials.json')
        os.chmod(destination / 'credentials.json', 0o600)
        manifest = {name: digest(destination / name) for name in FILES}
        (destination / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n')
        os.chmod(destination / 'manifest.json', 0o600)
    finally:
        if was_running:
            compose('start', 'app')
    print(f'Private backup: {destination}')


def restore(destination):
    destination = destination.resolve(strict=True)
    manifest = json.loads((destination / 'manifest.json').read_text())
    if set(manifest) != set(FILES) or any(digest(destination / name) != manifest[name] for name in FILES):
        raise RuntimeError('Backup checksum failed')
    with tarfile.open(destination / 'recordings-and-keys.tar.gz') as archive:
        for item in archive:
            path = Path(item.name)
            if path.is_absolute() or '..' in path.parts or not path.parts or path.parts[0] not in {'recordings', 'keys'} or item.issym() or item.islnk():
                raise RuntimeError('Unsafe archive member')
    running = compose('ps', '--status', 'running', '--services', capture_output=True, text=True).stdout.split()
    if 'app' in running:
        raise RuntimeError('Stop app first: docker compose --env-file .local/lab.env stop app')
    tables = compose('exec', '-T', 'postgres', 'psql', '-U', 'assessment', '-d', 'assessment', '-Atc',
                     "SELECT count(*) FROM information_schema.tables WHERE table_schema NOT IN ('pg_catalog','information_schema');",
                     capture_output=True, text=True).stdout.strip()
    if tables != '0':
        raise RuntimeError('Restore requires an empty database; restore into a fresh lab, never overwrite accepted work')
    existing = compose('run', '--rm', '--no-deps', '-T', '--user', '0:0', '--entrypoint', 'find', 'app',
                       '/var/lib/assessment/recordings', '/var/lib/assessment/keys', '-type', 'f', '-print', '-quit',
                       capture_output=True, text=True).stdout.strip()
    if existing:
        raise RuntimeError('Restore requires empty recording/key volumes; existing private files will never be overwritten')
    with (destination / 'database.sql').open('rb') as source:
        compose('exec', '-T', 'postgres', 'psql', '-U', 'assessment', '-d', 'assessment', '-v', 'ON_ERROR_STOP=1', '--single-transaction',
                stdin=source, stdout=None)
    with (destination / 'recordings-and-keys.tar.gz').open('rb') as source:
        compose('run', '--rm', '--no-deps', '-T', '--user', '0:0', '--entrypoint', 'tar', 'app', '-C', '/var/lib/assessment', '-xzf', '-', stdin=source)
    shutil.copyfile(destination / 'credentials.json', LOCAL / 'credentials.json')
    os.chmod(LOCAL / 'credentials.json', 0o600)
    print('Restored database, recordings, session keys and private seed credentials. Start app and verify a receipt and private recording.')


if __name__ == '__main__':
    try:
        if sys.argv[1] == 'backup':
            backup()
        elif sys.argv[1] == 'restore' and len(sys.argv) == 3:
            restore(Path(sys.argv[2]))
        else:
            raise RuntimeError('Usage: backup.py backup | restore BACKUP_DIRECTORY')
    except Exception as error:
        print(f'Backup command failed: {error}', file=sys.stderr)
        sys.exit(1)
