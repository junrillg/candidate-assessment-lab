#!/usr/bin/env python3
"""Export editorial SVGs and render Mermaid serially with the existing browser."""
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[1]
FRONTEND = ROOT / 'frontend'
LOCAL = ROOT / '.local'

def export_editorial(source, target):
    html = source.with_suffix('.html')
    if not html.is_file():
        return False
    # Editorial figures keep their layout in one self-contained inline SVG.
    figures = re.findall(r'<svg\b[^>]*>.*?</svg>', html.read_text(encoding='utf-8'), re.DOTALL)
    if len(figures) != 1 or ET.fromstring(figures[0]).tag != '{http://www.w3.org/2000/svg}svg':
        raise ValueError(f'{html.name} must contain exactly one namespaced SVG')
    target.write_text(figures[0] + '\n', encoding='utf-8')
    return True


def main():
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
        if export_editorial(source, target):
            print(target.relative_to(ROOT))
            continue
        subprocess.run(['npx', '--yes', '--package', '@mermaid-js/mermaid-cli@11.12.0', 'mmdc',
                        '-i', source, '-o', target, '-b', 'transparent', '-p', configuration,
                        '-c', ROOT / 'scripts/mermaid.json'], cwd=ROOT, check=True,
                       env={**os.environ, 'PUPPETEER_SKIP_DOWNLOAD': 'true'})
        print(target.relative_to(ROOT))


if __name__ == '__main__':
    main()
