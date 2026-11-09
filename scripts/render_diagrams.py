#!/usr/bin/env python3
"""Render committed Mermaid sources serially through the official CLI container."""
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]
IMAGE = 'ghcr.io/mermaid-js/mermaid-cli/mermaid-cli:11.12.0'
for source in sorted((ROOT / 'docs/diagrams').glob('*.mmd')):
    target = source.with_suffix('.svg')
    subprocess.run(['docker', 'run', '--rm', '-v', f'{ROOT}:/repo', IMAGE,
                    '-i', '/repo/' + str(source.relative_to(ROOT)), '-o', '/repo/' + str(target.relative_to(ROOT)),
                    '-b', 'transparent', '-p', '/repo/scripts/puppeteer.json'], check=True)
    print(target.relative_to(ROOT))
