#!/bin/sh
# Startet Claude Terminal mit dem System-Electron (Wayland oder X11 automatisch)
exec electron --ozone-platform-hint=auto /usr/lib/claude-terminal "$@"
