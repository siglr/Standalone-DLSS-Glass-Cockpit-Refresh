# Packaging and signing

Prepare all future standalone releases from this repository. Commit and push the corresponding source and version changes here; do not build releases from an older parent project or exported copy.

For a local build, run `python -B scripts/dev_package.py`. It uses a separate local DPAPI-protected development key, embeds only the public half, disables official automatic updates, and generates a development ZIP. It does not install the mod or publish files. Keep the private key out of Git and release archives.

The production public verification key is intentionally public in `standalone/ReleasePublicKey.cs`. Its private counterpart is not in this repository. Publishing an official release requires that private key, `GLASS_ENABLE_OFFICIAL_UPDATES=ON`, the production public-key source, and an explicit `GLASS_SIGNING_KEY` environment variable naming the protected private-key file. Then `python -B standalone/package.py` creates the signed archive, inventory, feed and version-specific hashes under build. It does not upload them. Publish the matching ZIP first and signed version JSON last.

Forks must use their own signing key and keep official app updates disabled unless implementing their own update channel. Do not label a fork as an official WeSimGlide release. If adding a fork channel, change the endpoint restrictions, product identity, key and branding coherently, and test upgrade/rollback against that fork's previous versions. Published releases must never be silently replaced under the same version.

The website source lives in `standalone/site/Standalone-DLSS-Glass-Cockpit-Refresh.html`. Official packaging copies it into `build/standalone/server-upload` alongside the ZIP, SHA-256 sidecar, version-specific hashes and signed version JSON. Upload the ZIP first, then the hashes and HTML, and the signed version JSON last. The page's Other tools link uses the server's adjacent `index.html`; do not replace that tools index with the historical mod redirect. Website-only edits do not require an app rebuild or version change. The HTML remains outside the app ZIP.

DPAPI CurrentUser keys depend on the Windows account/profile and machine protection. Copying the encrypted file alone is not portable recovery. Keep a protected, tested account/system backup before changing Windows or release machines. Do not silently generate a replacement for an established release key. Local release signatures are not Windows Authenticode certificates or antivirus clearance.
