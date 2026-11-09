#!/bin/bash
set -euo pipefail
umask 077

prepare_data_disk() {
  local device="$1" target="$2" fstab="$3" filesystem signatures size uuid contents mounted=false mountpoints source
  test "$(lsblk -nro TYPE "$device")" = disk
  mountpoints=$(lsblk -nro MOUNTPOINTS "$device")
  if mountpoint -q "$target"; then
    source=$(findmnt -n -o SOURCE --mountpoint "$target")
    test "$(readlink -f "$source")" = "$(readlink -f "$device")"
    test "$mountpoints" = "$target"
    mounted=true
  else
    test -z "$mountpoints" || { echo 'Data disk is already in use; refusing to format or remount it.' >&2; return 1; }
  fi
  signatures=$(wipefs --no-act --noheadings --output TYPE "$device")
  if filesystem=$(blkid --probe -s TYPE -o value "$device"); then
    test "$filesystem" = ext4 || { echo 'Existing disk is not ext4; refusing to format it.' >&2; return 1; }
    test "$signatures" = ext4 || { echo 'Conflicting disk signatures found; refusing to use it.' >&2; return 1; }
  else
    test "$?" = 2 || { echo 'Disk probe failed; refusing to format.' >&2; return 1; }
    test "$mounted" = false || { echo 'Mounted disk has no verified filesystem; refusing to format.' >&2; return 1; }
    test -z "$signatures" || { echo 'Existing disk signatures found; refusing to format.' >&2; return 1; }
    size=$(blockdev --getsize64 "$device")
    test "$size" -gt 0
    # A successful signature probe is insufficient for damaged/unknown data; only an entirely zero disk is initialized.
    echo 'Verifying the entire new data disk is zero-filled; this first initialization can take several minutes.'
    cmp -n "$size" "$device" /dev/zero || { echo 'Disk contains data or could not be read; refusing to format.' >&2; return 1; }
    mkfs.ext4 "$device"
  fi
  mkdir -p "$target"
  if ! "$mounted"; then
    contents=$(ls -A "$target")
    test -z "$contents" || { echo 'Mount directory contains files; refusing to hide them.' >&2; return 1; }
  fi
  uuid=$(blkid -s UUID -o value "$device")
  test -n "$uuid"
  awk -v target="$target" -v uuid="UUID=$uuid" '$2 == target && $1 != uuid {bad=1} END {exit bad}' "$fstab"
  if ! grep -Fq "UUID=$uuid $target " "$fstab"; then
    printf 'UUID=%s %s ext4 defaults,nofail 0 2\n' "$uuid" "$target" >>"$fstab"
  fi
  if ! "$mounted"; then mount "$device" "$target"; fi
}

main() {
  if test "$(id -u)" != 0; then echo 'Run this reviewed bootstrap through sudo in your SSH session.' >&2; return 1; fi
  local source commit device app temporary
  source=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
  commit=$(git -C "$source" rev-parse HEAD)
  [[ "$commit" =~ ^[0-9a-f]{40}$ ]]
  device=/dev/disk/azure/scsi1/lun0
  for attempt in $(seq 1 120); do
    if test -b "$device"; then break; fi
    sleep 2
  done
  test -b "$device" || { echo 'Azure data LUN0 is not attached; finish Terraform apply before bootstrap.' >&2; return 1; }
  prepare_data_disk "$device" /srv/candidate-assessment /etc/fstab
  systemctl stop docker.service docker.socket
  mkdir -p /srv/candidate-assessment/docker /etc/docker /etc/systemd/system/docker.service.d
  printf '{"data-root":"/srv/candidate-assessment/docker"}\n' >/etc/docker/daemon.json
  printf '[Unit]\nRequiresMountsFor=/srv/candidate-assessment\n' >/etc/systemd/system/docker.service.d/storage.conf
  systemctl daemon-reload
  systemctl enable --now docker
  app=/srv/candidate-assessment/app
  if ! test -d "$app/.git"; then git clone https://github.com/junrillg/candidate-assessment-lab.git "$app"; fi
  git -C "$app" fetch origin "$commit"
  git -C "$app" checkout --detach "$commit"
  temporary=$(mktemp)
  trap 'rm -f "$temporary"' RETURN
  curl --fail --location --retry 3 https://ollama.com/install.sh -o "$temporary"
  OLLAMA_VERSION=0.40.15 sh "$temporary"
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
  cd "$app"
  ollama pull llama3.2:latest
  make up
  printf 'Candidate Assessment running from %s. Use the Terraform SSH tunnel command for localhost:5188.\n' "$commit"
  printf 'Private login credentials: %s/.local/credentials.json; do not publish them.\n' "$app"
}

if [[ "${BASH_SOURCE[0]}" = "$0" ]]; then main "$@"; fi
