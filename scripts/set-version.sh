#!/usr/bin/env bash
# Bump the version everywhere that cannot read ./VERSION itself.
# Usage: scripts/set-version.sh X.Y.Z [YYYY-MM-DD]
set -euo pipefail
cd "$(dirname "$0")/.."
ver=${1:?usage: set-version.sh X.Y.Z [date]}
date=${2:-$(date +%F)}
[[ $ver =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "not X.Y.Z" >&2; exit 1; }
printf '%s\n' "$ver" > VERSION
sed -i "s/MARKETING_VERSION = [^;]*;/MARKETING_VERSION = $ver;/" macos/QuadClicker.xcodeproj/project.pbxproj
# Add a new <release> entry on top unless it already exists.
if ! grep -q "<release version=\"$ver\"" linux/assets/quadclicker.appdata.xml; then
  sed -i "0,/<releases>/s||<releases>\n    <release version=\"$ver\" date=\"$date\"><description><p>Release $ver.</p></description></release>|" linux/assets/quadclicker.appdata.xml
fi
scripts/check-version.sh
