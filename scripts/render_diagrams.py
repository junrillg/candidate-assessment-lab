#!/usr/bin/env python3
"""Render editable Mermaid serially with the official CLI and existing Playwright Chromium."""
import json
import os
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
FRONTEND = ROOT / 'frontend'
LOCAL = ROOT / '.local'
LOCAL.mkdir(mode=0o700, exist_ok=True)
result = subprocess.run(['node', '--input-type=module', '-e',
                         "import { chromium } from 'playwright'; console.log(chromium.executablePath());"],
                        cwd=FRONTEND, capture_output=True, text=True, check=True)
browser = result.stdout.strip()
if not Path(browser).is_file():
    raise SystemExit('Install the existing browser dependency first: cd frontend && npm ci && npx playwright install chromium')
configuration = LOCAL / 'mermaid-puppeteer.json'
configuration.write_text(json.dumps({'executablePath': browser, 'args': ['--no-sandbox', '--disable-setuid-sandbox']}))
os.chmod(configuration, 0o600)
for source in sorted((ROOT / 'docs/diagrams').glob('*.mmd')):
    target = source.with_suffix('.svg')
    subprocess.run(['npx', '--yes', '--package', '@mermaid-js/mermaid-cli@11.12.0', 'mmdc',
                    '-i', source, '-o', target, '-b', 'transparent', '-p', configuration,
                    '-c', ROOT / 'scripts/mermaid.json'], cwd=ROOT, check=True,
                   env={**os.environ, 'PUPPETEER_SKIP_DOWNLOAD': 'true'})
    print(target.relative_to(ROOT))
