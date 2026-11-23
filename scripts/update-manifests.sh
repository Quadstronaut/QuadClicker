#!/usr/bin/env bash
# After a release is PUBLISHED, point the package manifests at it.
# Usage: scripts/update-manifests.sh vX.Y.Z path/to/SHA256SUMS.txt
# Rewrites bucket/quadclicker.json (Scoop) and packaging/aur/PKGBUILD. Review, then commit.
set -euo pipefail
cd "$(dirname "$0")/.."
tag=${1:?tag}; sums=${2:?SHA256SUMS.txt}
ver=${tag#v}
sum() { awk -v f="$1" '{n=$2; sub(/^\*/,"",n)} n==f {print $1}' "$sums"; }

exe=$(sum QuadClicker.exe);                         [[ -n $exe ]] || { echo "QuadClicker.exe not in $sums" >&2; exit 1; }
tgz=$(sum "quadclicker-$ver-linux-x86_64.tar.gz");  [[ -n $tgz ]] || { echo "linux tarball not in $sums" >&2; exit 1; }

sed -i \
  -e "s|^\(    \"version\": \"\).*\(\",\)|\1$ver\2|" \
  -e "s|^\(    \"url\": \".*/releases/download/\)v[^/]*\(/QuadClicker.exe\",\)|\1$tag\2|" \
  -e "s|^\(    \"hash\": \"sha256:\).*\(\",\)|\1$exe\2|" \
  bucket/quadclicker.json
sed -i \
  -e "s|^pkgver=.*|pkgver=$ver|" \
  -e "s|^pkgrel=.*|pkgrel=1|" \
  -e "s|^sha256sums=.*|sha256sums=('$tgz')|" \
  packaging/aur/PKGBUILD
echo "updated for $tag"
