# Packaging (QCLK-9 wave 1)

Maintainer reference. Install instructions for users are in the README.

## Decisions (ticket QCLK-9, comment 12458, section 10)

Operator on 2026-11-23: "Start wave 1". Wave 1 = everything that needs no code-signing identity, no paid certificate, no new third-party account. Where the ticket left an item open, the recommendation was followed:

| # | Open item | Choice in this change |
|:--|:--|:--|
| 1 | Order vs STAR-306 | Build workflow and own packaging now. No catalogue submissions. |
| 2 | Windows signing | Out of wave 1. Binaries unsigned and documented as such. |
| 3 | Org and package identity | Unchanged (`Quadstronaut/QuadClicker`, `com.quadstronaut.quadclicker`). Revisit with STAR-306: a rename means touching `bucket/`, `packaging/aur/`, `README.md`, and the repo base URL in `packaging/repo/`. |
| 4 | apt/dnf hosting | Own signed static repos built in CI. Default URL `https://quadstronaut.github.io/QuadClicker/{apt,rpm}` (GitHub Pages of this repo; no new repo). Enabling Pages is an operator step. |
| 5 | GPG custody | Offline primary key, signing subkey as CI secrets. Key not created here. |
| 6 | macOS | Unsigned zip attached to the draft only, job is non-blocking, documented as never run on a real Mac. No tap. |
| 7 | Flatpak/Snap | Skipped. |
| 8 | Windows updater | Kept for direct downloads; auto-off for installs under Scoop/Chocolatey/winget paths (`PackageManagerInstall`). |
| 9 | Microsoft Store | Not in wave 1. |
| 10 | Official Homebrew / Scoop Extras | Not pursued; own Scoop bucket is this repo (`bucket/`). |

Also decided here:

- **Version source:** `VERSION` (0.2.0, the Windows project's number). csproj and CMake read it directly; `scripts/set-version.sh X.Y.Z` syncs Xcode and AppStream; `scripts/check-version.sh` (run first in the workflow) fails on drift or tag mismatch.
- **Pre-release per push to master: dropped.** The workflow triggers on `vX.Y.Z` tags only.
- **Windows installer:** none (would need signing to be useful). Portable zip and bare exe. The bare `QuadClicker.exe` stays because the in-app updater and Scoop fetch that exact name.
- **Linux builds on Ubuntu 24.04** (the verified toolchain). The `.deb` depends on 24.04 library names; the `.rpm` builds natively in a Fedora container.
- **Release visibility:** the repo is public. The workflow only creates a *draft*; drafts are visible to maintainers only until Publish is clicked.

## Cutting a release

```bash
scripts/set-version.sh 0.3.0            # edits VERSION, Xcode, AppStream; runs the check
git commit -am "QCLK-9 Release 0.3.0"   # on a branch, merged via PR
git tag v0.3.0 && git push origin v0.3.0
```

The workflow opens a draft release with every asset and `SHA256SUMS.txt`. Review it, click **Publish**, then:

```bash
gh release download v0.3.0 -p SHA256SUMS.txt
scripts/update-manifests.sh v0.3.0 SHA256SUMS.txt   # Scoop manifest + AUR PKGBUILD
git diff && git commit -am "QCLK-9 Point manifests at v0.3.0"   # via PR
```

AUR: copy `packaging/aur/PKGBUILD` to the AUR git repo and run `makepkg --printsrcinfo > .SRCINFO` (operator, needs an AUR account).

## Operator steps (not done: they need your identity or secrets)

1. **GPG signing key** (for the apt/dnf repos). Run on a trusted machine, passphrase in your password manager:

   ```bash
   gpg --quick-generate-key "QuadClicker Releases <quadstronaut@users.noreply.github.com>" ed25519 cert never
   gpg --list-keys --keyid-format long          # note the primary KEYID
   gpg --quick-add-key <FULL-FINGERPRINT> ed25519 sign 2y
   gpg --list-secret-keys --keyid-format long   # note the signing subkey id, the one marked [S]
   # export ONLY the signing subkey (the "!" is required):
   gpg --armor --export-secret-subkeys <SUBKEY-ID>! > quadclicker-signing-subkey.asc
   gpg --armor --export <KEYID> > quadclicker-public.asc
   gpg --armor --gen-revoke <KEYID> > quadclicker-revoke.asc     # store offline
   ```

   Keep the primary secret key offline; never commit any `.asc` secret. Then:

   ```bash
   gh secret set QUADCLICKER_GPG_PRIVATE_KEY < quadclicker-signing-subkey.asc
   gh secret set QUADCLICKER_GPG_KEY_ID --body "<SUBKEY-ID>!"
   ```

   Until both secrets exist the `repos` job skips itself with a notice; the `.deb`/`.rpm` files are still released.
2. **Publish the repos.** The `repos` job attaches `quadclicker-repos-<ver>.tar.gz` (trees `apt/` and `rpm/`). Put them on the branch GitHub Pages serves:

   ```bash
   git worktree add ../qclk-pages gh-pages   # create the branch once with: git checkout --orphan gh-pages
   tar -xzf quadclicker-repos-0.2.0.tar.gz -C ../qclk-pages
   ```

   Then repo Settings, Pages, deploy from `gh-pages`. Each release's tree contains only that release's packages; to keep older versions, merge into the existing `pool/` and `x86_64/` before running the builders locally (`packaging/repo/build-*-repo.sh <dir-with-all-packages> <out>`).
3. **AUR account** and SSH key (aur.archlinux.org), then push the PKGBUILD.
4. **Review and publish each draft** (check the macOS zip is acceptable, or delete it from the draft).

## Unproven until the workflow runs on GitHub

- `.rpm` build and install in the Fedora container; RPM repo creation (`createrepo_c`).
- AppImage build and run (linuxdeploy downloads, Qt plugin deployment, icon). Tools are fetched unpinned.
- macOS build output path and zip.
- Signed apt/dnf repos end to end (no key exists to test with).
- `.deb` installed on a clean Ubuntu 24.04 (only built and inspected on Debian 13).
- Scoop `autoupdate` hash regex against a real `SHA256SUMS.txt`.
- AUR package build (`makepkg` not available here).
- Draft-release job (`gh release create`).
