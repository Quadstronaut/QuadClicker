#!/usr/bin/env bash
# Build a static apt repository from .deb files.
# Usage: packaging/repo/build-apt-repo.sh <debs-dir> <out-dir>
# Env:   GPG_KEY_ID   signing key (must already be in the keyring). Unset = UNSIGNED layout (test only).
#        GNUPGHOME    keyring location, as usual.
# Layout (serve <out-dir> over HTTPS):
#   pool/main/q/quadclicker/*.deb
#   dists/stable/{Release,InRelease,Release.gpg}, dists/stable/main/binary-amd64/Packages(.gz)
#   quadclicker-archive-keyring.gpg      <- users put this in /etc/apt/keyrings/
# Needs: apt-utils (apt-ftparchive, dpkg-scanpackages), gzip, gpg when signing.
set -euo pipefail
debs=$(cd "${1:?debs-dir}" && pwd)
out=$(mkdir -p "${2:?out-dir}" && cd "$2" && pwd)

pool="$out/pool/main/q/quadclicker"
bin="$out/dists/stable/main/binary-amd64"
mkdir -p "$pool" "$bin"
cp -f "$debs"/*.deb "$pool/"

# Paths in Packages are relative to the repo root, so run from there.
( cd "$out"
  dpkg-scanpackages --arch amd64 pool/main /dev/null > "dists/stable/main/binary-amd64/Packages"
  gzip -9nkf "dists/stable/main/binary-amd64/Packages"
  apt-ftparchive \
    -o APT::FTPArchive::Release::Origin=QuadClicker \
    -o APT::FTPArchive::Release::Label=QuadClicker \
    -o APT::FTPArchive::Release::Suite=stable \
    -o APT::FTPArchive::Release::Codename=stable \
    -o APT::FTPArchive::Release::Architectures=amd64 \
    -o APT::FTPArchive::Release::Components=main \
    release dists/stable > dists/stable/Release.tmp
  mv dists/stable/Release.tmp dists/stable/Release )

if [[ -n ${GPG_KEY_ID:-} ]]; then
  gpg --batch --yes --local-user "$GPG_KEY_ID" --armor --detach-sign -o "$out/dists/stable/Release.gpg" "$out/dists/stable/Release"
  gpg --batch --yes --local-user "$GPG_KEY_ID" --clearsign -o "$out/dists/stable/InRelease" "$out/dists/stable/Release"
  gpg --batch --yes --export "$GPG_KEY_ID" > "$out/quadclicker-archive-keyring.gpg"   # binary keyring for signed-by=
  echo "apt repo signed with $GPG_KEY_ID"
else
  echo "WARNING: GPG_KEY_ID unset, apt repo is UNSIGNED (layout test only, do not publish)" >&2
fi
