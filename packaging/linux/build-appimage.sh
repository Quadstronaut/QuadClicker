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

# Third-party build tools, not pinned by hash (continuous releases); output is verified by checksum, not by these.
curl -fsSL -o "$tools/linuxdeploy" \
  https://github.com/linuxdeploy/linuxdeploy/releases/download/continuous/linuxdeploy-x86_64.AppImage
curl -fsSL -o "$tools/linuxdeploy-plugin-qt" \
  https://github.com/linuxdeploy/linuxdeploy-plugin-qt/releases/download/continuous/linuxdeploy-plugin-qt-x86_64.AppImage
curl -fsSL -o "$tools/linuxdeploy-plugin-appimage"   https://github.com/linuxdeploy/linuxdeploy-plugin-appimage/releases/download/continuous/linuxdeploy-plugin-appimage-x86_64.AppImage
chmod +x "$tools/linuxdeploy" "$tools/linuxdeploy-plugin-qt" "$tools/linuxdeploy-plugin-appimage"

appdir="$tools/AppDir"
mkdir -p "$appdir"
cp -a "$stage/usr" "$appdir/usr"
# System files (udev, modules-load) cannot be installed by an AppImage; drop them.
rm -f "$appdir/usr/lib/udev/rules.d/70-quadclicker-uinput.rules" "$appdir/usr/lib/modules-load.d/quadclicker.conf"

export APPIMAGE_EXTRACT_AND_RUN=1      # no FUSE on CI runners
export PATH="$tools:$PATH"
export QMAKE=${QMAKE:-$(command -v qmake6)}
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
