#!/usr/bin/env bash
# Wrap the staged install into an AppImage. CI only (downloads linuxdeploy).
# Usage: packaging/linux/build-appimage.sh <stage-dir> <out-dir>
#   stage-dir = the tree build.sh staged (contains usr/)
# Run on the OLDEST distro you want to support: an AppImage runs on its build glibc and newer.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
stage=$(cd "${1:?stage-dir}" && pwd)
out=$(mkdir -p "${2:?out-dir}" && cd "$2" && pwd)
ver=$(tr -d '[:space:]' < "$root/VERSION")
tools=$(mktemp -d)

# Third-party build tools, pinned to dated releases and verified by SHA256 (hashes taken
# 2026-11-23 from a download of each release asset over HTTPS). The AppImage we ship is
# separately covered by SHA256SUMS.txt, which only proves the download matches the release.
fetch() { # <name> <url> <sha256>
  curl -fsSL -o "$tools/$1" "$2"
  echo "$3  $tools/$1" | sha256sum -c - || { echo "checksum mismatch for $1" >&2; exit 1; }
  chmod +x "$tools/$1"
}
gh=https://github.com/linuxdeploy
fetch linuxdeploy "$gh/linuxdeploy/releases/download/1-alpha-20250213-2/linuxdeploy-x86_64.AppImage" \
  4648f278ab3ef31f819e67c30d50f462640e5365a77637d7e6f2ad9fd0b4522a
fetch linuxdeploy-plugin-qt "$gh/linuxdeploy-plugin-qt/releases/download/1-alpha-20250213-1/linuxdeploy-plugin-qt-x86_64.AppImage" \
  15106be885c1c48a021198e7e1e9a48ce9d02a86dd0a1848f00bdbf3c1c92724
fetch linuxdeploy-plugin-appimage "$gh/linuxdeploy-plugin-appimage/releases/download/1-alpha-20250213-1/linuxdeploy-plugin-appimage-x86_64.AppImage" \
  992d502a248e14ab185448ddf6f6e7d25558cb84d4623c354c3af350c25fccb3

appdir="$tools/AppDir"
mkdir -p "$appdir"
cp -a "$stage/usr" "$appdir/usr"
# System files (udev, modules-load) cannot be installed by an AppImage; drop them.
rm -f "$appdir/usr/lib/udev/rules.d/70-quadclicker-uinput.rules" "$appdir/usr/lib/modules-load.d/quadclicker.conf"

export APPIMAGE_EXTRACT_AND_RUN=1      # no FUSE on CI runners
export PATH="$tools:$PATH"
export QMAKE=${QMAKE:-$(command -v qmake6)}
# appimage plugin runs appstreamcli on the metainfo and fails the build on ANY warning (v0.2.1 run 45783788760).
# Metainfo is still validated for Linux packages elsewhere; do not let a warning block the AppImage.
export LDAI_NO_APPSTREAM=1
export LDAI_OUTPUT="$out/QuadClicker-$ver-x86_64.AppImage"
export LINUXDEPLOY_OUTPUT_VERSION="$ver"

( cd "$tools" && ./linuxdeploy --appdir "$appdir" --plugin qt \
    --executable "$appdir/usr/bin/quadclicker" \
    --desktop-file "$appdir/usr/share/applications/quadclicker.desktop" \
    --icon-file "$appdir/usr/share/icons/hicolor/scalable/apps/quadclicker.svg" \
    --output appimage )

# linuxdeploy names the file itself; normalise to our release name.
if [[ ! -f $LDAI_OUTPUT ]]; then
  produced=$(ls "$tools"/*.AppImage 2>/dev/null | head -1 || true)
  [[ -n $produced ]] || { echo "no AppImage produced" >&2; exit 1; }
  mv "$produced" "$LDAI_OUTPUT"
fi
ls -l "$out"
