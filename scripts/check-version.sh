#!/usr/bin/env bash
# One version source: ./VERSION. Fails if a file that cannot read it has drifted,
# or (with a tag argument) if the git tag is not exactly v<VERSION>.
# Usage: scripts/check-version.sh [vX.Y.Z]
set -euo pipefail
cd "$(dirname "$0")/.."

ver=$(tr -d '[:space:]' < VERSION)
[[ $ver =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "VERSION '$ver' is not X.Y.Z" >&2; exit 1; }
bad=0
fail() { echo "MISMATCH: $1" >&2; bad=1; }

if [[ -n ${1:-} && $1 != "v$ver" ]]; then fail "tag $1 != v$ver (VERSION)"; fi

# macOS: every MARKETING_VERSION in the Xcode project, and Info.plist must defer to it.
while read -r v; do
  [[ $v == "$ver" ]] || fail "macos/QuadClicker.xcodeproj MARKETING_VERSION = $v"
done < <(sed -n 's/.*MARKETING_VERSION = \([^;]*\);.*/\1/p' macos/QuadClicker.xcodeproj/project.pbxproj | tr -d '\r')
grep -q 'MARKETING_VERSION' macos/QuadClicker/Resources/Info.plist || fail "Info.plist does not use \$(MARKETING_VERSION)"

# Linux AppStream: newest <release> must be this version.
first=$(sed -n 's/.*<release version="\([^"]*\)".*/\1/p' linux/assets/quadclicker.appdata.xml | head -1)
[[ $first == "$ver" ]] || fail "appdata.xml newest release = $first"

# Windows (csproj) and Linux (CMake) read ./VERSION directly.
grep -q '\.\./VERSION' windows/QuadClicker.csproj || fail "windows/QuadClicker.csproj does not read ../VERSION"
grep -q '\.\./VERSION' linux/CMakeLists.txt        || fail "linux/CMakeLists.txt does not read ../VERSION"

[[ $bad == 0 ]] && echo "version OK: $ver"
exit $bad
