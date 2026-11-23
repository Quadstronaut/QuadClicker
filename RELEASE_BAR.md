# Release bar (QCLK-9, wave 1)

QuadClicker is a personal tool, not a Starhold product: no storefront, no pricing, no paid accounts. This file is the repo's `## Release bar` (the untracked local `CLAUDE.md` is gitignored, so the bar lives here).

## Done for this release means

1. One version, in `VERSION`. Windows, Linux, macOS, AppStream and the tag all agree.
2. Pushing tag `vX.Y.Z` runs one workflow that builds Windows (exe + portable zip), Linux (`.deb`, `.tar.gz`, AppImage, `.rpm`) and an unsigned macOS zip, writes `SHA256SUMS.txt`, and attaches everything to a **draft** GitHub release. Nothing publishes itself. Repo visibility never changes.
3. Package files exist in the repo: Scoop bucket manifest (`bucket/`), AUR PKGBUILD, apt/dnf repository builders, `.rpm` spec.
4. README tells each platform how to install, and says plainly that binaries are unsigned.
5. Proof command (runs on any machine with bash): `scripts/check-version.sh && dotnet test windows/Tests/QuadClicker.Tests.csproj -c Release`. The full proof is the workflow going green on a real tag and the draft containing every asset.

## Out of scope for wave 1

- Code signing of any kind (Windows Authenticode/Azure/SignPath, Apple Developer ID and notarization). Wave 2.
- winget, Chocolatey, Microsoft Store, Homebrew (tap or core), Scoop Extras, Flatpak, Snap, COPR, OBS, nixpkgs, MacPorts, Launchpad PPA, Debian/Fedora proper, Mac App Store. Waves 2 and 3, or never.
- A Windows installer (needs signing to be worth shipping).
- Creating the GPG key, accounts, or any new GitHub repo. Operator steps: `docs/PACKAGING.md`.
- Moving or renaming the repo (STAR-306).
