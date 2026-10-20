# Packaging and signing

## Current official-DLL release: 1.0.2.16

Build from this checkout using `python -B scripts/official_release_package.py --enable-official-updates --shared-site "C:\WSG MSFS 2024 FG Switcher\dist\website"`, with `GLASS_SIGNING_KEY` explicitly set to the existing protected production key. Never generate a replacement key. The app is compiled with the pinned production public key and official updates enabled. Both IFreemz graphics files remain byte-unchanged and must match the release-note checksums. Development builds use their separate key and disable official app updates.

Website files are staged in `build/standalone/server-upload`; maintainer instructions, validation and listing copy are in `build/release-preparation`. The user ZIP includes one concise README.txt, required licenses/notices and both corresponding graphics source archives under Source code. Developer reports stay in the repository. The full app/source ZIP is a separate download.

Public filenames never contain a version: `Standalone-DLSS-Glass-Cockpit-Refresh.zip`, `.version.json`, `.hashes.json`, and `Standalone-DLSS-Glass-Cockpit-Refresh-source.zip`, with SHA-256 sidecars. Versions belong in signed metadata, the app and GitHub tags. No WSG release-history folder is needed.

### Older automatic updaters

The published v1.0.2.10 release supplies immutable compatibility files, pinned by `standalone/compatibility-lock.json`. Packaging reuses and verifies all ten assets; it never rebuilds or re-signs their packages, feeds or sources.

- Before v1.0.2: receive the original signed v1.0.2 legacy package.
- v1.0.2 (including normalized 1.0.2.0): receive the original signed v1.0.2.1 bridge, retaining its original graphics bytes.
- v1.0.2.1 and later, including v1.0.2.10: receive the current release directly.

The fixed app URLs use internal Apache rewrites selected by product/version User-Agent; redirects are rejected by older clients. Keep `compatibility/` available. Existing users open the app normally; it may reopen more than once, without uninstalling. A normal browser receives the current ZIP.

### Shared website and screenshots

Start from the latest `C:\WSG MSFS 2024 FG Switcher\dist\website\index.html`, containing OpenXR FG Switcher, and check for newer live edits before deployment. Change only the Glass Cockpit Refresh card in the staged index. Update its description, screenshot dimensions and hash query; preserve other cards byte for byte. Stage only this tool's assets, never other tools' packages. The local source folder is read-only to this packager.

Merge `standalone/site/legacy-updates.conf` into the shared .htaccess while preserving cache/CORS and other-tool rules. The packager replaces an existing marked compatibility block or appends it once. Pages revalidate and downloads/feeds use no-store. Screenshot filenames stay unversioned; image URLs use content-hash query strings.

### Required checks and publication

Run `python -B scripts/prepare_release_uploads.py` after signing to stage the two WSG upload bundles and the short HangarAddons changelog.

Run standalone discovery/installer, updater, official-engine, installation-mode, compatibility, dirty-buffer paint and development-package checks. Run `tests/release_migration_tests.py` against the production signed ZIP: real historical update chains, direct v1.0.2.10 upgrade, retained personal files/OFXR, exact production UI in both modes, immutable bridge checks, and staged route/filename checks. Run graphics fixtures sequentially only with the real simulator closed. The optional Dark screen fix ON glare failure is a disclosed upstream limitation; default-OFF, external-settings, startup and core checks must pass.

Prepare RELEASE-DESCRIPTION.md, RELEASE-NOTES.md and the user-approved screenshots with an accurate version caption for GitHub, WSG, Flightsim.to and HangarAddons. Preparation alone does not publish anything. Commit/push the exact source before creating release tag v1.0.2.16.

1. Publish current corresponding source and checksum on GitHub/WSG.
2. Retain or restore the exact pinned compatibility files and merge routing without dropping newer live rules.
3. Upload current ZIP, hashes, tool page, screenshots and the narrowly updated shared index.
4. Upload the signed main version JSON last.
5. Verify HTTP 200 without redirects for browser and User-Agents /0.1.9, /1.0.1, /1.0.2, /1.0.2.1, /1.0.2.10 and /1.0.2.14; compare signed hashes and no-store/Vary headers. These checks require deployment.
6. Publish the GitHub release and update the existing Flightsim.to/HangarAddons entries with the normal ZIP, full description, changelog. Preserve the four approved screenshots and all videos.

## Historical custom-engine packaging

Prepare all future standalone releases from this repository. Commit and push the corresponding source and version changes here; do not build releases from an older parent project or exported copy.

For a local build, run `python -B scripts/dev_package.py`. It uses a separate local DPAPI-protected development key, embeds only the public half, disables official automatic updates, and generates a development ZIP. It does not install the mod or publish files. Keep the private key out of Git and release archives.

The production public verification key is intentionally public in `standalone/ReleasePublicKey.cs`. Its private counterpart is not in this repository. Publishing an official release requires that private key, `GLASS_ENABLE_OFFICIAL_UPDATES=ON`, the production public-key source, and an explicit `GLASS_SIGNING_KEY` environment variable naming the protected private-key file. Then `python -B standalone/package.py` creates the signed archive, inventory, feed and fixed-name hashes under build. It does not upload them. Publish the matching source ZIP and source checksum first, then the binary ZIP, and the signed version JSON last.

Forks must use their own signing key and keep official app updates disabled unless implementing their own update channel. Do not label a fork as an official WeSimGlide release. If adding a fork channel, change the endpoint restrictions, product identity, key and branding coherently, and test upgrade/rollback against that fork's previous versions. Published releases must never be silently replaced under the same version.

The website source lives in `standalone/site/Standalone-DLSS-Glass-Cockpit-Refresh.html`. Official packaging copies it into `build/standalone/server-upload` alongside the ZIP, SHA-256 sidecar, fixed-name hashes and signed version JSON. Upload the matching source archive first, then the binary ZIP, hashes and HTML, and the signed version JSON last. The page's All tools link uses the server's adjacent `index.html`; do not replace that tools index with the historical mod redirect. Website-only edits do not require an app rebuild or version change. The HTML remains outside the app ZIP.

DPAPI CurrentUser keys depend on the Windows account/profile and machine protection. Copying the encrypted file alone is not portable recovery. Keep a protected, tested account/system backup before changing Windows or release machines. Do not silently generate a replacement for an established release key. Local release signatures are not Windows Authenticode certificates or antivirus clearance.




Historical releases used version-specific source archive names. Current releases use the fixed `Standalone-DLSS-Glass-Cockpit-Refresh-source.zip` and its SHA-256 sidecar alongside the binary ZIP. This exact source snapshot includes engine changes, shaders, dependencies and build scripts; its SOURCE-SNAPSHOT.json identifies the base commit and hashes every source file. Do not distribute the GPL graphics DLL without also supplying its corresponding source by a GPL-compliant method. The engine's license is documented in ENGINE-LICENSE.md; the separate C# application retains PolyForm Noncommercial. Preserve vendor/glass/LICENSE and vendor/glass/NOTICE.md in binary and source distributions.


## Release numbering

Standalone 1.0.2 is a transition release based on upstream 1.0.1. Previously installed updaters accept exactly three components; 1.0.2 accepts three or four. Starting with the next upstream release, use `upstream-major.upstream-minor.upstream-patch.package-revision`, beginning with revision 1 (for example 1.0.2.1), and increment the last component for standalone-only changes. Numeric ordering must remain increasing; do not publish 1.0.1.1 after 1.0.2.

Before switching the live channel to four components, retain a separately named signed 1.0.2 ZIP/feed and provide a legacy-client transition route. An older client that skips 1.0.2 still rejects four-part metadata. Do not overwrite the shared feed with a four-part version until that migration is implemented and tested, or explicitly document a manual update for those legacy clients. Build metadata normalizes three-part versions to a fourth component of zero; four-part versions retain their revision.


Before publishing a graphics update, run `python -B tests/package_startup_tests.py build/standalone/server-upload/Standalone-DLSS-Glass-Cockpit-Refresh.zip` against the signed package. This extracts only its graphics DLLs into disposable build fixtures and checks native/bootstrap startup with both normal and copied swapchain tables. Also use `tests/official_package_tests.py OLD_ZIP NEW_FEED NEW_ZIP [OLD_AppUpdate.cs]`; the optional historical updater source verifies compatibility with the updater already in users' hands.
