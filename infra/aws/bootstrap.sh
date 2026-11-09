#!/bin/bash
# Explicit cloud-lab application bootstrap after reviewing a commit-pinned checkout.
set -euo pipefail
umask 077
if test "$(id -u)" != 0; then echo 'Run through sudo from the SSM session.' >&2; exit 1; fi
APP=/srv/candidate-assessment/app
test -d "$APP/.git"
mountpoint -q /srv/candidate-assessment
cd "$APP"
COMMIT=$(git rev-parse HEAD)
if ! printf '%s' "$COMMIT" | grep -Eq '^[0-9a-f]{40}$'; then exit 1; fi
# Official vendor installer, pinned runtime version; review its source before first execution.
TEMP=$(mktemp)
trap 'rm -f "$TEMP"' EXIT
curl --fail --location --retry 3 https://ollama.com/install.sh -o "$TEMP"
OLLAMA_VERSION=0.40.15 sh "$TEMP"
mkdir -p /srv/candidate-assessment/ollama /etc/systemd/system/ollama.service.d
chown ollama:ollama /srv/candidate-assessment/ollama
cat >/etc/systemd/system/ollama.service.d/assessment.conf <<'UNIT'
[Unit]
RequiresMountsFor=/srv/candidate-assessment
[Service]
Environment="OLLAMA_HOST=0.0.0.0:11434"
Environment="OLLAMA_MODELS=/srv/candidate-assessment/ollama"
UNIT
systemctl daemon-reload
systemctl restart ollama
for attempt in $(seq 1 60); do
  if curl -fsS http://localhost:11434/api/tags >/dev/null; then break; fi
  sleep 2
done
ollama pull llama3.2:latest
make up
printf 'Candidate Assessment running from %s. Use SSM port forwarding to localhost:5188.\n' "$COMMIT"
printf 'Private login credentials are in %s/.local/credentials.json; do not publish them.\n' "$APP"
