#!/usr/bin/env bash
# Claude Terminal – Installer für Arch Linux
# Danach startest du die App mit:  c4
set -euo pipefail

REPO="https://github.com/nullfuenfdrei/claude-terminal.git"

echo ">> Pakete installieren (base-devel, git, electron) ..."
sudo pacman -S --needed --noconfirm base-devel git electron

here="$(cd "$(dirname "${BASH_SOURCE[0]:-$0}")" 2>/dev/null && pwd || true)"
if [ -n "$here" ] && [ -f "$here/PKGBUILD" ]; then
  cd "$here"                                   # aus einem geklonten Repo gestartet
else
  tmp="$(mktemp -d)"; trap 'rm -rf "$tmp"' EXIT
  echo ">> Code holen ..."
  git clone --depth 1 "$REPO" "$tmp/claude-terminal"
  cd "$tmp/claude-terminal/linux"
fi

echo ">> Paket bauen und installieren ..."
makepkg -sif --noconfirm

echo
echo "✓ Claude Terminal ist installiert."
echo "  Starten:  c4   (oder im App-Menü: Claude Terminal)"
