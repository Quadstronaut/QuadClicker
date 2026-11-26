#!/usr/bin/env bash
# Build the Linux binary, stage it, and produce the tarball and the .deb.
# Usage: packaging/linux/build.sh <out-dir> [work-dir]
#   out-dir   receives quadclicker-<ver>-linux-x86_64.tar.gz and quadclicker_<ver>_amd64.deb
#   work-dir  scratch (build tree + staging). Defaults to a fresh mktemp -d. Reused if given.
# Needs: cmake, ninja, Qt6 dev, libxtst-dev, libxss-dev, dpkg-dev (dpkg-deb, dpkg-shlibdeps), fakeroot optional.
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../.." && pwd)
out=$(mkdir -p "${1:?out-dir}" && cd "$1" && pwd)
work=${2:-$(mktemp -d)}
mkdir -p "$work"
ver=$(tr -d '[:space:]' < "$root/VERSION")
stage="$work/stage"

cmake -S "$root/linux" -B "$work/build" -G Ninja -DCMAKE_BUILD_TYPE=Release \
      -DCMAKE_INSTALL_PREFIX=/usr -DQUADCLICKER_BUILD_TESTS=OFF
cmake --build "$work/build"

# Fresh stage tree every run (only ever our own $work/stage).
mkdir -p "$stage"
find "$stage" -mindepth 1 -delete
DESTDIR="$stage" cmake --install "$work/build" --strip
install -Dm644 "$root/LICENSE" "$stage/usr/share/doc/quadclicker/LICENSE"

# ---- tarball (also the AUR -bin source) ----
tar --owner=0 --group=0 --numeric-owner --sort=name -C "$stage" \
    -czf "$out/quadclicker-$ver-linux-x86_64.tar.gz" usr

# ---- .deb ----
pkg="$work/deb"
mkdir -p "$pkg"
find "$pkg" -mindepth 1 -delete
cp -a "$stage/usr" "$pkg/usr"
install -d "$pkg/DEBIAN"
# Debian policy: licence lives at usr/share/doc/<pkg>/copyright.
mv "$pkg/usr/share/doc/quadclicker/LICENSE" "$pkg/usr/share/doc/quadclicker/copyright"

# Let dpkg compute the shared-library dependencies (handles the t64 renames across releases).
shl="$work/shlibs"
mkdir -p "$shl/debian"
: > "$shl/debian/control"
depends=$(cd "$shl" && dpkg-shlibdeps -O -e "$stage/usr/bin/quadclicker" 2>/dev/null | sed -n 's/^shlibs:Depends=//p')
[[ -n $depends ]] || { echo "dpkg-shlibdeps produced no Depends" >&2; exit 1; }
size=$(du -sk "$pkg/usr" | cut -f1)

cat > "$pkg/DEBIAN/control" <<EOF
Package: quadclicker
Version: $ver
Section: utils
Priority: optional
Architecture: amd64
Installed-Size: $size
Depends: $depends
Recommends: libxcb-cursor0
Maintainer: Quadstronaut <noreply@users.noreply.github.com>
Homepage: https://github.com/Quadstronaut/QuadClicker
Description: Native open-source auto-clicker (Qt6, X11 and Wayland)
 Configurable click rate, button and type, fixed or cursor position, stop
 conditions, idle start, global hotkeys (X11), tray icon and a full CLI.
 On Wayland, clicks go through /dev/uinput; this package loads the uinput
 module at boot and installs a udev rule for the logged-in user.
EOF

cat > "$pkg/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if [ "$1" = configure ]; then
    modprobe uinput 2>/dev/null || true
    if command -v udevadm >/dev/null 2>&1; then
        udevadm control --reload 2>/dev/null || true
        udevadm trigger --subsystem-match=misc --sysname-match=uinput 2>/dev/null || true
    fi
fi
exit 0
EOF
chmod 0755 "$pkg/DEBIAN/postinst"

dpkg-deb --root-owner-group --build "$pkg" "$out/quadclicker_${ver}_amd64.deb"
echo "built: $out"
ls -l "$out"
