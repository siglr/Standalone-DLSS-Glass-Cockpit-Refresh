# Security and application behavior — v0.1.9

> Source repository: default source builds disable automatic official updates and use a separate local key when packaged with `scripts/dev_package.py`. The network/update behavior below describes official releases. Source builds are not claimed to reproduce official binaries byte for byte.

This document describes the standalone package so users and antivirus reviewers can assess its behavior. It is not a safety certification. Antivirus alerts have been reported for v0.1.8; Microsoft review was requested and other vendor submissions are being handled separately. No vendor clearance for v0.1.9 is claimed. Keep protection enabled and leave detected files blocked during review.

## Executable and graphics DLLs

The Windows Forms launcher is ordinary .NET Framework C# with no packer or obfuscation added by this project. Discovery and install/uninstall run directly in the app. It does not start PowerShell, command scripts or encoded commands. Four legacy .ps1 filenames remain only because older updaters require their presence; each stops with a message directing the user to the EXE. They contain no installer code and are never executed by the app.

The graphics DLLs intercept MSFS D3D12/upscaler rendering so the glass fix can redraw instrument content. The dxgi.dll proxy is loaded through the normal game-directory DLL mechanism and forwards system DXGI exports. Selected ReShade components are built in. Hooking and proxy DLL loading can look sensitive to security software; those capabilities are necessary for this rendering fix and have not been disguised. The upstream source versions/licenses are recorded in UPSTREAM.md and upstream-lock.json. The official v0.1.9 rendering binaries were unchanged from v0.1.8. Locally compiled binaries may differ.

## Files and privileges

- Setup accepts a local folder containing FlightSimulator2024.exe, refuses protected WindowsApps and linked/reparse paths, and requires MSFS to be closed.
- Its simulator payload is restricted to dxgi.dll, StandaloneGlassGraphics.dll and StandaloneGlass.ini. It keeps .StandaloneGlass-install.json, optional original files in .StandaloneGlass-backup, and temporary .StandaloneGlass-transaction recovery data.
- Existing conflicting files require explicit replacement consent and are backed up. Updates preserve the complete installed INI and original uninstall backups. Uninstall restores originals and saves changed settings beside the app; unexpectedly changed DLLs or damaged backups stop recovery.
- Setup tries normal user permissions. If access is denied, the UI offers a UAC prompt to run the same EXE for the selected install/uninstall operation and folder. It does not elevate the self-updater or silently change permissions/ownership.
- Live controls edit the mod's INI. SimulatorDirectory.txt remembers the selected folder beside the external app. Existing renderer diagnostics can remain beside MSFS; this release adds no logging, capture or reporting feature.
- The launcher does not add services, scheduled tasks, startup entries, antivirus exclusions or firewall rules. It does not modify OFXR settings, OpenXR registration, headset files or original MSFS executables.

## Network access and automatic updates

At launch the app requests this fixed HTTPS version file:
https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.version.json

A newer accepted version downloads only this fixed HTTPS ZIP:
https://wesimglide.org/otherdata/tools/Standalone-DLSS-Glass-Cockpit-Refresh.zip

Redirects and alternate addresses are rejected. The updater enforces size/expansion limits, checks the ZIP hash, verifies a signed per-file inventory, and rejects unsafe paths, links, duplicate names and unlisted files. No settings, simulator files or reports are uploaded. Requests expose the normal client IP and a product/version User-Agent to the web server; ordinary server access logs may record them.

About opens the fixed HTTPS mod webpage in the user's default browser. That page fetches adjacent release metadata and the WSG logo. Its local ZIP checker uses browser SHA-256; selected file bytes are not uploaded. VirusTotal links are lookups, not automatic submissions.

The external package update uses .app-update for staging and recovery. The helper is a verified copy of the existing app; it waits for that app to exit, replaces only inventoried files, then reopens the new app. It preserves unrelated user files and stops on modified managed files. Recovery records are checked against signed old/new inventories. Staging is cleaned after success or recovery. It never installs new game DLLs automatically: close MSFS and use Update when those DLLs actually differ.

## Signatures, checksums and limitations

Release metadata and inventories use RSA-3072/SHA-256 signatures (PKCS#1 v1.5). The launcher pins the publisher's public key; the private signing key is not in any distributed file or on the download server. A changed release or file list cannot be authorized merely by changing the server's SHA-256 field. Invalid/missing signatures leave the existing app in place.

This is separate from Windows Authenticode: this release's EXE has no publicly trusted code-signing certificate. Local release signing does not establish Windows publisher reputation, scan the binaries for malware, or prove the signed code is harmless. A compromised build/signing account or an attacker already able to replace the running launcher is outside this protection. First installation still depends on obtaining the genuine package. A website checksum alone cannot protect against a compromised website publishing both a malicious ZIP and a matching checksum.

The first upgrade from v0.1.4–v0.1.8 uses those versions' existing HTTPS/checksum validation; they cannot retroactively enforce signatures. v0.1.9 and later enforce them for subsequent updates. Users may instead manually extract the complete official release with the app closed, preserving personal settings files.

Version-specific hashes identify the exact ZIP, EXE and DLLs. VirusTotal pages may be absent or show differing results over time. Neither a matching hash nor a clean scan guarantees safety. No guarantee is made that these changes will remove antivirus detections.
