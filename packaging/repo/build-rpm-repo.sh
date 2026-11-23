#!/usr/bin/env bash
# Build a static dnf/yum repository from .rpm files.
# Usage: packaging/repo/build-rpm-repo.sh <rpms-dir> <out-dir> [base-url]
# Env:   GPG_KEY_ID   signing key in the keyring. Unset = UNSIGNED layout (test only).
# Layout (serve <out-dir> over HTTPS):
#   x86_64/*.rpm, x86_64/repodata/{repomd.xml,repomd.xml.asc,...}
#   quadclicker.repo     <- users copy to /etc/yum.repos.d/
#   RPM-GPG-KEY-quadclicker
# Needs: createrepo_c, gpg when signing. rpm --addsign is NOT used: repo_gpgcheck signs repomd.xml.
set -euo pipefail
rpms=$(cd "${1:?rpms-dir}" && pwd)
out=$(mkdir -p "${2:?out-dir}" && cd "$2" && pwd)
base=${3:-https://quadstronaut.github.io/QuadClicker/rpm}

mkdir -p "$out/x86_64"
cp -f "$rpms"/*.rpm "$out/x86_64/"
createrepo_c --update "$out/x86_64"

if [[ -n ${GPG_KEY_ID:-} ]]; then
  gpg --batch --yes --local-user "$GPG_KEY_ID" --armor --detach-sign \
      -o "$out/x86_64/repodata/repomd.xml.asc" "$out/x86_64/repodata/repomd.xml"
  gpg --batch --yes --armor --export "$GPG_KEY_ID" > "$out/RPM-GPG-KEY-quadclicker"
  check=1
  echo "rpm repo signed with $GPG_KEY_ID"
else
  check=0
  echo "WARNING: GPG_KEY_ID unset, rpm repo is UNSIGNED (layout test only, do not publish)" >&2
fi

cat > "$out/quadclicker.repo" <<EOF
[quadclicker]
name=QuadClicker
baseurl=$base/x86_64
enabled=1
gpgcheck=0
repo_gpgcheck=$check
gpgkey=$base/RPM-GPG-KEY-quadclicker
EOF
# gpgcheck=0: the .rpm files themselves are not signed (only repository metadata is);
# repo_gpgcheck=1 makes dnf verify repomd.xml, whose checksums cover every package.
