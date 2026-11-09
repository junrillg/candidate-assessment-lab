#!/usr/bin/env python3
"""Local lab entry point; every write is scoped to this Compose project."""
import argparse
import contextlib
import fcntl
import json
import os
from pathlib import Path
import secrets
import shutil
import subprocess
import sys
import time
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
LOCAL = ROOT / '.local'
ENV = LOCAL / 'lab.env'
PROJECT = 'candidate-assessment-lab'


def run(args, **kwargs):
    kwargs.setdefault('cwd', ROOT)
    return subprocess.run([str(value) for value in args], check=True, **kwargs)


def compose(*args, **kwargs):
    if not ENV.exists():
        raise RuntimeError('Run make configure first')
    return run(['docker', 'compose', '--env-file', ENV, '-p', PROJECT, *args], **kwargs)


def private_write(path, text):
    fd = os.open(path, os.O_WRONLY | os.O_CREAT | os.O_EXCL, 0o600)
    with os.fdopen(fd, 'w') as output:
        output.write(text)


def configure():
    LOCAL.mkdir(mode=0o700, exist_ok=True)
    if not ENV.exists():
        private_write(ENV, 'POSTGRES_PASSWORD=' + secrets.token_urlsafe(32) + '\nOLLAMA_MODEL=llama3.2:latest\n')
    credentials = LOCAL / 'credentials.json'
    if not credentials.exists():
        users = [
            ('11111111-1111-1111-1111-111111111111', 'candidate1@assessment.local', 'Alex Morgan', 'Candidate'),
            ('22222222-2222-2222-2222-222222222222', 'candidate2@assessment.local', 'Jordan Lee', 'Candidate'),
            ('33333333-3333-3333-3333-333333333333', 'reviewer@assessment.local', 'Sam Rivera', 'Reviewer'),
        ]
        private_write(credentials, json.dumps({'users': [dict(id=i, email=e, name=n, role=r, password=secrets.token_urlsafe(24) + '!aA9')
                                                        for i, e, n, r in users]}, indent=2) + '\n')
    print('Private local configuration ready. Open .local/credentials.json locally for login; passwords are never printed.')


def validate():
    if not ENV.exists() or not (LOCAL / 'credentials.json').exists():
        raise RuntimeError('Run make configure first')
    for path in [ENV, LOCAL / 'credentials.json']:
        if path.stat().st_mode & 0o077:
            raise RuntimeError(f'{path.name} must be private: chmod 600 it')
    users = json.loads((LOCAL / 'credentials.json').read_text()).get('users', [])
    if len(users) != 3 or any(len(user.get('password', '')) < 20 for user in users):
        raise RuntimeError('Expected three private seed users with generated passwords')
    compose('config', '--quiet')
    print('Configuration valid; no secrets printed.')


def wait_ready(seconds=120):
    deadline = time.monotonic() + seconds
    while time.monotonic() < deadline:
        try:
            with urllib.request.urlopen('http://127.0.0.1:5188/health/ready', timeout=3) as response:
                if response.status == 200:
                    print('Candidate Assessment ready: http://localhost:5188')
                    return
        except Exception:
            time.sleep(2)
    raise RuntimeError('Application did not become ready; run make logs')


@contextlib.contextmanager
def heavy_lock():
    LOCAL.mkdir(mode=0o700, exist_ok=True)
    with (LOCAL / 'heavy-check.lock').open('w') as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise RuntimeError('Another lab build or verification is running; wait for it') from error
        yield


def unit_tests():
    run([sys.executable, '-m', 'unittest', 'discover', '-s', 'tests/Integration', '-p', 'test_*.py'])
    run([sys.executable, '-m', 'unittest', 'discover', '-s', 'infra/transcription', '-p', 'test_*.py'])
    projects = sorted((ROOT / 'tests').rglob('*.csproj'))
    for project in projects:
        run(['dotnet', 'test', project, '--nologo', '-m:1'])
    run(['npm', 'ci'], cwd=ROOT / 'frontend')
    run(['npm', 'test'], cwd=ROOT / 'frontend')


def terraform(*args, profile='local'):
    run(['terraform', f'-chdir=infra/{profile}', *args])


def main():
    parser = argparse.ArgumentParser(description='Candidate Assessment local operations. Deployment defaults to local; azure-apply and aws-apply explicitly provision paid cloud resources.')
    parser.add_argument('command', choices=['configure', 'validate', 'up', 'down', 'logs', 'status', 'migrate', 'seed', 'sample', 'test',
                                          'integration', 'recovery', 'verify', 'diagrams', 'backup', 'restore', 'deploy-init', 'deploy-validate',
                                          'deploy-plan', 'deploy-apply', 'deploy-down', 'aws-init', 'aws-validate', 'aws-plan', 'aws-apply',
                                          'azure-init', 'azure-validate', 'azure-plan', 'azure-apply'])
    parser.add_argument('--backup', type=Path, help='Existing private backup directory; required for restore')
    args = parser.parse_args()
    os.chdir(ROOT)
    if args.command == 'configure':
        configure()
    elif args.command == 'validate':
        validate()
    elif args.command == 'up':
        with heavy_lock():
            configure()
            validate()
            compose('build', '--no-cache' if os.environ.get('LAB_NO_CACHE') == '1' else '--pull', 'app', 'transcription',
                    env={**os.environ, 'COMPOSE_PARALLEL_LIMIT': '1'})
            compose('up', '-d', '--wait', '--wait-timeout', '600', 'postgres', 'kafka', 'transcription')
            compose('run', '--rm', '--no-deps', 'migrate')
            compose('run', '--rm', '--no-deps', 'seed')
            compose('up', '-d', '--no-build', 'app')
            wait_ready()
    elif args.command == 'down':
        compose('down', '--remove-orphans')
        print('Stopped. PostgreSQL, Kafka, recordings, models, and session keys remain in volumes.')
    elif args.command == 'logs':
        compose('logs', '--tail=100', '-f')
    elif args.command == 'status':
        compose('ps')
    elif args.command in {'migrate', 'seed'}:
        compose('run', '--rm', '--no-deps', args.command)
    elif args.command in {'sample', 'integration'}:
        run([sys.executable, 'tests/Integration/journey.py', args.command])
    elif args.command == 'recovery':
        with heavy_lock():
            run([sys.executable, 'tests/Integration/recovery.py'])
    elif args.command == 'test':
        with heavy_lock():
            unit_tests()
    elif args.command == 'verify':
        with heavy_lock():
            validate()
            unit_tests()
            run(['dotnet', 'build', 'src/Host/Host.csproj', '--nologo', '-m:1'])
            run(['npm', 'run', 'build'], cwd=ROOT / 'frontend')
            terraform('init', '-backend=false', '-input=false')
            terraform('fmt', '-check')
            terraform('validate')
            terraform('init', '-backend=false', '-input=false', profile='aws')
            terraform('fmt', '-check', profile='aws')
            terraform('validate', profile='aws')
            terraform('init', '-backend=false', '-input=false', profile='azure')
            terraform('fmt', '-check', profile='azure')
            terraform('validate', profile='azure')
            terraform('test', profile='azure')
            run([sys.executable, 'tests/Integration/journey.py', 'integration'])
    elif args.command == 'diagrams':
        with heavy_lock():
            run([sys.executable, 'scripts/render_diagrams.py'])
    elif args.command == 'backup':
        with heavy_lock():
            run([sys.executable, 'scripts/backup.py', 'backup'])
    elif args.command == 'restore':
        if not args.backup:
            parser.error('restore requires --backup DIR')
        with heavy_lock():
            run([sys.executable, 'scripts/backup.py', 'restore', args.backup])
    elif args.command == 'azure-init':
        terraform('init', '-input=false', profile='azure')
    elif args.command == 'azure-validate':
        terraform('fmt', '-check', profile='azure')
        terraform('validate', profile='azure')
        terraform('test', profile='azure')
    elif args.command == 'azure-plan':
        LOCAL.mkdir(mode=0o700, exist_ok=True)
        terraform('plan', '-input=false', '-out=../../.local/azure.tfplan', profile='azure')
    elif args.command == 'azure-apply':
        terraform('apply', '../../.local/azure.tfplan', profile='azure')
    elif args.command == 'aws-init':
        terraform('init', '-input=false', profile='aws')
    elif args.command == 'aws-validate':
        terraform('fmt', '-check', profile='aws')
        terraform('validate', profile='aws')
    elif args.command == 'aws-plan':
        terraform('plan', '-input=false', '-out=../../.local/aws.tfplan', profile='aws')
    elif args.command == 'aws-apply':
        terraform('apply', '../../.local/aws.tfplan', profile='aws')
    elif args.command == 'deploy-init':
        terraform('init', '-input=false')
    elif args.command == 'deploy-validate':
        terraform('fmt', '-check')
        terraform('validate')
    elif args.command == 'deploy-plan':
        terraform('plan', '-input=false', '-out=../../.local/local.tfplan')
    elif args.command == 'deploy-apply':
        terraform('apply', '../../.local/local.tfplan')
    elif args.command == 'deploy-down':
        terraform('destroy')


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, subprocess.CalledProcessError) as error:
        print(f'Lab command failed: {error}', file=sys.stderr)
        sys.exit(1)
