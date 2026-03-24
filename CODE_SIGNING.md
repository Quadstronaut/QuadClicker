# Code Signing Guide — QuadClicker

**Author:** Kyle Green (Quadstronaut)
**Last updated:** 2026-03-23

This document details the steps required to code-sign QuadClicker binaries on all three platforms. Code signing is required to:
- Prevent Windows SmartScreen from blocking the installer/EXE
- Pass macOS Gatekeeper and notarization checks
- Provide GPG-verified downloads on Linux

---

## Table of Contents

1. [Windows — Authenticode Signing](#1-windows--authenticode-signing)
2. [macOS — Developer ID + Notarization](#2-macos--developer-id--notarization)
3. [Linux — GPG Signing](#3-linux--gpg-signing)
4. [GitHub Actions Integration](#4-github-actions-integration)
5. [Certificate Renewal Checklist](#5-certificate-renewal-checklist)

---

## 1. Windows — Authenticode Signing

Windows requires an **Authenticode** code-signing certificate. For SmartScreen reputation to build quickly, an **Extended Validation (EV)** certificate is strongly recommended over a standard OV cert.

### 1.1 Obtain a Certificate

**Option A — EV Certificate (Recommended)**
- Purchase from a Microsoft-trusted CA:
  - [DigiCert](https://www.digicert.com/signing/code-signing-certificates) — ~$500/yr, most common
  - [Sectigo](https://sectigo.com/ssl-certificates-tls/code-signing) — ~$250/yr
  - [GlobalSign](https://www.globalsign.com/en/code-signing-certificate/)
- EV certs require identity verification (business or individual) — have your ID/company docs ready
- EV certs are delivered on a **hardware USB token** (SafeNet/eToken) — you must have the physical token to sign

**Option B — OV Certificate (Cheaper, slower trust)**
- Same CAs above, ~$100–200/yr
- Delivered as a PFX file — easier for CI/CD but SmartScreen trust builds slowly (months of downloads needed)

**Option C — Azure Trusted Signing (recommended for CI/CD)**
- Microsoft's cloud HSM signing service — no physical token needed
- [https://learn.microsoft.com/en-us/azure/trusted-signing/](https://learn.microsoft.com/en-us/azure/trusted-signing/)
- ~$9.99/month — most practical for automated pipelines

### 1.2 Sign the Binary Locally (PFX method)

```powershell
# Install Windows SDK (signtool.exe is included)
# Or use: dotnet tool install --global AzureSignTool

signtool sign `
  /fd SHA256 `
  /tr http://timestamp.digicert.com `
  /td SHA256 `
  /f "path\to\certificate.pfx" `
  /p "YOUR_PFX_PASSWORD" `
  "bin\Release\net10.0-windows\QuadClicker.exe"
```

### 1.3 Sign the Binary (Azure Trusted Signing)

```powershell
# Install AzureSignTool
dotnet tool install --global AzureSignTool

AzureSignTool sign `
  --azure-key-vault-url "https://YOUR-VAULT.vault.azure.net/" `
  --azure-key-vault-client-id "YOUR_CLIENT_ID" `
  --azure-key-vault-client-secret "YOUR_CLIENT_SECRET" `
  --azure-key-vault-tenant-id "YOUR_TENANT_ID" `
  --azure-key-vault-certificate "YOUR_CERT_NAME" `
  --timestamp-rfc3161 "http://timestamp.acs.microsoft.com" `
  --timestamp-digest sha256 `
  --file-digest sha256 `
  "bin\Release\net10.0-windows\QuadClicker.exe"
```

### 1.4 Verify the Signature

```powershell
signtool verify /pa /v "QuadClicker.exe"
```

### 1.5 Required GitHub Secrets (Windows)

| Secret name | Value |
|---|---|
| `WINDOWS_CERT_PFX_BASE64` | Base64-encoded PFX file: `[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx"))` |
| `WINDOWS_CERT_PASSWORD` | PFX password |
| *(or for Azure)* | |
| `AZURE_TENANT_ID` | Azure AD tenant ID |
| `AZURE_CLIENT_ID` | App registration client ID |
| `AZURE_CLIENT_SECRET` | App registration secret |
| `AZURE_KEY_VAULT_URL` | Key Vault URI |
| `AZURE_KEY_VAULT_CERT_NAME` | Certificate name in Key Vault |

---

## 2. macOS — Developer ID + Notarization

macOS requires two steps: **signing** with a Developer ID certificate and **notarization** through Apple's servers. Unsigned or unnotarized apps are blocked by Gatekeeper on all modern macOS versions.

### 2.1 Enroll in Apple Developer Program

1. Go to [https://developer.apple.com/programs/](https://developer.apple.com/programs/)
2. Enroll as an individual ($99/year) or organization
3. Complete identity verification (takes 24–48 hours for orgs)

### 2.2 Create a Developer ID Certificate

1. Open **Xcode → Settings → Accounts → Manage Certificates**
2. Click **+** → select **Developer ID Application**
3. Xcode creates and installs the certificate in your Keychain
4. Export for CI: **Keychain Access → find cert → Export as .p12** (set a password)

Or via command line:
```bash
# List available signing identities
security find-identity -v -p codesigning

# You'll see something like:
# "Developer ID Application: Your Name (TEAM_ID)"
```

### 2.3 Sign the App Bundle

```bash
# Sign the .app bundle
codesign \
  --deep \
  --force \
  --verify \
  --verbose \
  --sign "Developer ID Application: Kyle Green (YOUR_TEAM_ID)" \
  --options runtime \
  --entitlements entitlements.plist \
  QuadClicker.app

# Verify
codesign --verify --deep --strict --verbose=2 QuadClicker.app
spctl --assess --type exec -vv QuadClicker.app
```

**Minimum entitlements.plist for QuadClicker:**
```xml
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>com.apple.security.cs.allow-jit</key>
    <false/>
    <key>com.apple.security.automation.apple-events</key>
    <true/>
    <!-- Accessibility permission for CGEventPost -->
    <key>com.apple.security.temporary-exception.mach-lookup.global-name</key>
    <array>
        <string>com.apple.accessibility.AXServer</string>
    </array>
</dict>
</plist>
```

### 2.4 Notarize the App

Apple's notarization service scans the binary for malware and issues a ticket. Required for distribution outside the Mac App Store.

```bash
# Package as DMG or zip first
hdiutil create -volname QuadClicker -srcfolder QuadClicker.app -ov -format UDZO QuadClicker.dmg

# Submit for notarization (requires Apple ID with app-specific password)
xcrun notarytool submit QuadClicker.dmg \
  --apple-id "your@appleid.com" \
  --team-id "YOUR_TEAM_ID" \
  --password "YOUR_APP_SPECIFIC_PASSWORD" \
  --wait

# Staple the notarization ticket to the DMG
xcrun stapler staple QuadClicker.dmg

# Verify
xcrun stapler validate QuadClicker.dmg
spctl --assess --type open --context context:primary-signature -v QuadClicker.dmg
```

**App-specific password:** Generate at [https://appleid.apple.com](https://appleid.apple.com) → Security → App-Specific Passwords

### 2.5 Required GitHub Secrets (macOS)

| Secret name | Value |
|---|---|
| `MACOS_CERT_P12_BASE64` | Base64-encoded .p12: `base64 -i cert.p12 \| tr -d '\n'` |
| `MACOS_CERT_PASSWORD` | .p12 export password |
| `MACOS_NOTARYTOOL_APPLE_ID` | Your Apple ID email |
| `MACOS_NOTARYTOOL_TEAM_ID` | 10-character team ID from developer.apple.com |
| `MACOS_NOTARYTOOL_APP_PASSWORD` | App-specific password from appleid.apple.com |

### 2.6 macOS Accessibility Permission

QuadClicker uses `CGEventPost` to inject mouse events. macOS requires the app to be granted **Accessibility** permission in **System Settings → Privacy & Security → Accessibility**. The app should:
1. Detect if it lacks the permission on launch
2. Show a clear dialog directing the user to grant it
3. Use `AXIsProcessTrustedWithOptions` to check and prompt

---

## 3. Linux — GPG Signing

Linux package managers use GPG to verify package authenticity. There is no mandatory signing requirement like Windows/macOS, but it is standard practice for professional distributions.

### 3.1 Generate a GPG Key (if you don't have one)

```bash
gpg --full-generate-key
# Choose: RSA and RSA, 4096 bits, key does not expire (or set expiry)
# Real name: Kyle Green
# Email: your@email.com
# Comment: QuadClicker signing key
```

### 3.2 Export and Publish Your Public Key

```bash
# Get your key ID
gpg --list-secret-keys --keyid-format LONG

# Export public key
gpg --armor --export YOUR_KEY_ID > quadclicker-signing-key.asc

# Publish to keyservers
gpg --keyserver keyserver.ubuntu.com --send-keys YOUR_KEY_ID
gpg --keyserver keys.openpgp.org --send-keys YOUR_KEY_ID
```

Host the public key at a well-known URL, e.g.:
`https://github.com/Quadstronaut/QuadClicker/releases/download/signing-key/quadclicker-signing-key.asc`

### 3.3 Sign Release Artifacts

```bash
# Sign a file (detached signature)
gpg --armor --detach-sign QuadClicker-linux-x86_64.tar.gz
# Produces: QuadClicker-linux-x86_64.tar.gz.asc

# Sign the .deb package
dpkg-sig --sign builder quadclicker_1.0.0_amd64.deb

# Verify
gpg --verify QuadClicker-linux-x86_64.tar.gz.asc QuadClicker-linux-x86_64.tar.gz
```

### 3.4 APT Repository Signing (for apt distribution)

If hosting a custom APT repository:
```bash
# Generate Release file and sign it
apt-ftparchive release . > Release
gpg --default-key YOUR_KEY_ID -abs -o Release.gpg Release
gpg --default-key YOUR_KEY_ID --clearsign -o InRelease Release
```

### 3.5 Required GitHub Secrets (Linux)

| Secret name | Value |
|---|---|
| `GPG_PRIVATE_KEY` | ASCII-armored private key: `gpg --armor --export-secret-keys YOUR_KEY_ID` |
| `GPG_PASSPHRASE` | Passphrase for the GPG key |

---

## 4. GitHub Actions Integration

### Windows signing in CI (Azure Trusted Signing recommended)

```yaml
- name: Sign Windows binary
  run: |
    dotnet tool install --global AzureSignTool
    AzureSignTool sign \
      --azure-key-vault-url "${{ secrets.AZURE_KEY_VAULT_URL }}" \
      --azure-key-vault-client-id "${{ secrets.AZURE_CLIENT_ID }}" \
      --azure-key-vault-client-secret "${{ secrets.AZURE_CLIENT_SECRET }}" \
      --azure-key-vault-tenant-id "${{ secrets.AZURE_TENANT_ID }}" \
      --azure-key-vault-certificate "${{ secrets.AZURE_KEY_VAULT_CERT_NAME }}" \
      --timestamp-rfc3161 "http://timestamp.acs.microsoft.com" \
      --timestamp-digest sha256 \
      --file-digest sha256 \
      windows/bin/Release/net10.0-windows/QuadClicker.exe
```

### macOS signing + notarization in CI

```yaml
- name: Import macOS certificate
  run: |
    echo "${{ secrets.MACOS_CERT_P12_BASE64 }}" | base64 --decode > cert.p12
    security create-keychain -p "" build.keychain
    security import cert.p12 -k build.keychain -P "${{ secrets.MACOS_CERT_PASSWORD }}" -T /usr/bin/codesign
    security set-key-partition-list -S apple-tool:,apple: -s -k "" build.keychain
    security list-keychain -d user -s build.keychain

- name: Sign and notarize
  run: |
    codesign --deep --force --verify --verbose \
      --sign "Developer ID Application: Kyle Green (${{ secrets.MACOS_NOTARYTOOL_TEAM_ID }})" \
      --options runtime \
      --entitlements macos/entitlements.plist \
      QuadClicker.app
    hdiutil create -volname QuadClicker -srcfolder QuadClicker.app -ov -format UDZO QuadClicker.dmg
    xcrun notarytool submit QuadClicker.dmg \
      --apple-id "${{ secrets.MACOS_NOTARYTOOL_APPLE_ID }}" \
      --team-id "${{ secrets.MACOS_NOTARYTOOL_TEAM_ID }}" \
      --password "${{ secrets.MACOS_NOTARYTOOL_APP_PASSWORD }}" \
      --wait
    xcrun stapler staple QuadClicker.dmg
```

### Linux GPG signing in CI

```yaml
- name: Sign Linux artifacts
  run: |
    echo "${{ secrets.GPG_PRIVATE_KEY }}" | gpg --batch --import
    echo "${{ secrets.GPG_PASSPHRASE }}" | gpg --batch --yes --passphrase-fd 0 \
      --armor --detach-sign QuadClicker-linux-x86_64.tar.gz
```

---

## 5. Certificate Renewal Checklist

| Platform | Cert Type | Typical Validity | Action |
|---|---|---|---|
| Windows (Azure Trusted Signing) | Cloud HSM cert | 3 years | Renew in Azure portal before expiry |
| Windows (PFX/EV) | EV Code Signing | 1–3 years | Purchase renewal from CA, re-export PFX, update GitHub secret |
| macOS | Developer ID | While enrolled | Annual $99 Apple Developer Program renewal keeps cert valid |
| Linux | GPG key | No expiry (or set expiry) | If expiry set, run `gpg --edit-key` → `expire` before expiry date |

**Important:** Signing a binary with an expired certificate will cause verification failures on end-user machines. Set calendar reminders 60 days before expiry.

---

## Notes for Kyle

- **macOS Accessibility:** CGEventPost requires Accessibility permission. Apple does NOT allow this in the Mac App Store — QuadClicker must be distributed outside the store (direct DMG or Homebrew cask). This is by design.
- **Windows EV USB token:** EV certs require the physical USB token to be present for signing. For CI/CD, Azure Trusted Signing is the modern alternative — highly recommended.
- **Legal entity name:** The company name you use on the Windows EV cert and Apple Developer account should be consistent. Decide this before purchasing certificates.
- **App Bundle ID (macOS):** You'll need a Bundle ID before notarization. Suggested: `com.quadstronaut.quadclicker` — update once the real company/product name is finalized.
