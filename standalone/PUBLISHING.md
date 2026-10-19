# Packaging and signing

## Current official-DLL release: 1.0.2.10

Build from this checkout using `python -B scripts/official_release_package.py --enable-official-updates --shared-site "C:\WSG MSFS 2024 FG Switcher\dist\website"`, with `GLASS_SIGNING_KEY` explicitly set to the existing protected production key. Never generate a replacement key. The script compiles only the app; IFreemz's official DLL stays unchanged. Its ZIP and DLL hashes must match the official release notes. Development packages remain isolated and have official app updates disabled.

The latest shared index is now `C:\WSG MSFS 2024 FG Switcher\dist\website\index.html`. It includes OpenXR FG Switcher and currently matches the live index byte for byte. Read it and the adjacent `.htaccess` as the current baseline; do not fall back to the older Stream Feeder copy or overwrite another project's files. The release script copies this index unchanged and appends only this tool's compatibility routing to a staged `.htaccess`.

Output: `build/standalone/server-upload`. The regular ZIP/feed are v1.0.2.10. Public filenames never contain the version; version numbers stay in metadata and release tags. Current source uses `Standalone-DLSS-Glass-Cockpit-Refresh-source.zip`, and file hashes use `Standalone-DLSS-Glass-Cockpit-Refresh.hashes.json`. Historical releases remain on GitHub. Compatibility assets live under `compatibility/` and must remain available:

- `compatibility/*-legacy.zip` and `.version.json`: unchanged published v1.0.2 for apps that cannot parse four-part versions.
- `compatibility/*-bridge.zip` and `.version.json`: app-only transition based on v1.0.2, retaining its exact graphics bytes and adding support for official-DLL payloads.
- Corresponding 1.0.2 and 1.0.2.1 source ZIPs/checksums, plus the final source ZIP/checksum.

The server internally rewrites the fixed app URLs according to their product/version User-Agent. HTTP redirects are unsuitable because the updater rejects them. Apps older than 1.0.2 receive the original transition first, then the bridge, then 1.0.2.10. Apps at 1.0.2 need only the bridge and final release. Normal browser downloads receive the final release. No uninstall is required. Older apps may reopen more than once.

Required release checks: `tests/standalone_tests.py`, `tests/app_update_tests.py`, `tests/official_engine_tests.py`, `tests/development_package_tests.py`, and `tests/release_migration_tests.py`. The latter exercises real signed archives with the historical updater sources and the production EXE, preserving user files and OFXR. Run graphics tests only with the real simulator closed. The documented optional Dark screen fix ON glare failure is an upstream limitation of the unmodified DLL, not a passing check. Default-OFF, external-settings, startup and core checks must pass.

Keep only current downloads on WSG. Older releases and matching source remain on GitHub; the server does not need a release-history folder. Retain only the compatibility packages needed by older automatic updaters.

### Publication order

1. Publish corresponding source archives and checksums on GitHub and WSG, together with the versioned release notes. GitHub/Flightsim.to/HangarAddons receive the final ZIP, not either compatibility ZIP.
2. Upload legacy and bridge ZIPs/feeds/checksums to WSG. Keep the old shared feed until the routing is installed.
3. Merge/apply the staged `.htaccess`, preserving any newer live rules. Confirm fixed feed and ZIP URLs return HTTP 200 (not redirects) for User-Agents ending in `/0.1.9`, `/1.0.1`, `/1.0.2`, `/1.0.2.1` and `/1.0.2.10`. They must route to legacy, legacy, bridge, current and current respectively. Confirm `Cache-Control: no-store` and `Vary: User-Agent`.
4. Upload the final ZIP, source/hash files, tool page and latest index. Publish the final signed version JSON **last**. Recheck the normal browser download and every updater route against its signed checksum.
5. Publish the GitHub release and update Flightsim.to and HangarAddons with v1.0.2.10 and RELEASE-NOTES.md. Do not describe the installer as authored by IFreemz or the optional dark-screen test as passing.

Live routing/headers can only be confirmed after deployment. Preparing these files does not upload them. Old fixed-URL clients remain supported only while these compatibility assets/rules are retained.

After publishing, retain the exact legacy/bridge ZIPs, feeds and source. Never rebuild or replace them under the same version. The current packager is explicitly limited to 1.0.2.10; before preparing a later release, change it to reuse the immutable published compatibility assets.

## Historical custom-engine packaging

Prepare all future standalone releases from this repository. Commit and push the corresponding source and version changes here; do not build releases from an older parent project or exported copy.

For a local build, run `python -B scripts/dev_package.py`. It uses a separate local DPAPI-protected development key, embeds only the public half, disables official automatic updates, and generates a development ZIP. It does not install the mod or publish files. Keep the private key out of Git and release archives.

The production public verification key is intentionally public in `standalone/ReleasePublicKey.cs`. Its private counterpart is not in this repository. Publishing an official release requires that private key, `GLASS_ENABLE_OFFICIAL_UPDATES=ON`, the production public-key source, and an explicit `GLASS_SIGNING_KEY` environment variable naming the protected private-key file. Then `python -B standalone/package.py` creates the signed archive, inventory, feed and fixed-name hashes under build. It does not upload them. Publish the matching source ZIP and source checksum first, then the binary ZIP, and the signed version JSON last.

Forks must use their own signing key and keep official app updates disabled unless implementing their own update channel. Do not label a fork as an official WeSimGlide release. If adding a fork channel, change the endpoint restrictions, product identity, key and branding coherently, and test upgrade/rollback against that fork's previous versions. Published releases must never be silently replaced under the same version.

The website source lives in `standalone/site/Standalone-DLSS-Glass-Cockpit-Refresh.html`. Official packaging copies it into `build/standalone/server-upload` alongside the ZIP, SHA-256 sidecar, fixed-name hashes and signed version JSON. Upload the matching source archive first, then the binary ZIP, hashes and HTML, and the signed version JSON last. The page's All tools link uses the server's adjacent `index.html`; do not replace that tools index with the historical mod redirect. Website-only edits do not require an app rebuild or version change. The HTML remains outside the app ZIP.

DPAPI CurrentUser keys depend on the Windows account/profile and machine protection. Copying the encrypted file alone is not portable recovery. Keep a protected, tested account/system backup before changing Windows or release machines. Do not silently generate a replacement for an established release key. Local release signatures are not Windows Authenticode certificates or antivirus clearance.

The current shared tools index is `C:\WSG Stream Feeder\dist\upload\index.html`. Use that file as the starting point for any requested index changes. Do not use the historical redirect in the old mod repository or its upload folder. Standalone packaging leaves the shared index untouched.

Cache policy: merge `standalone/site/cache-headers.conf` into the shared `/otherdata/tools/.htaccess`, preserving existing rules and CORS settings. This snippet is also staged with the upload files; uploading it as `.conf` alone does not activate it. The combined `.htaccess` is maintained in `C:\WSG Stream Feeder\web\.htaccess` and staged in that project's `dist\upload\.htaccess`. The HTML must revalidate before reuse; the fixed ZIP and version-feed URLs use `no-store`. Styles/scripts are inline, and the page already fetches release metadata with `cache: 'no-store'`. After uploading, check the actual HTTP response headers. Previously cached pages may need one final hard refresh before the new policy takes effect. No app rebuild or new release is needed.


For v1.0.0 and later, publish the version-specific `*-source.zip` and its SHA-256 sidecar alongside the binary ZIP. This exact source snapshot includes engine changes, shaders, dependencies and build scripts; its SOURCE-SNAPSHOT.json identifies the base commit and hashes every source file. Do not distribute the GPL graphics DLL without also supplying its corresponding source by a GPL-compliant method. The engine's license is documented in ENGINE-LICENSE.md; the separate C# application retains PolyForm Noncommercial. Preserve vendor/glass/LICENSE and vendor/glass/NOTICE.md in binary and source distributions.


## Release numbering

Standalone 1.0.2 is a transition release based on upstream 1.0.1. Previously installed updaters accept exactly three components; 1.0.2 accepts three or four. Starting with the next upstream release, use `upstream-major.upstream-minor.upstream-patch.package-revision`, beginning with revision 1 (for example 1.0.2.1), and increment the last component for standalone-only changes. Numeric ordering must remain increasing; do not publish 1.0.1.1 after 1.0.2.

Before switching the live channel to four components, retain a separately named signed 1.0.2 ZIP/feed and provide a legacy-client transition route. An older client that skips 1.0.2 still rejects four-part metadata. Do not overwrite the shared feed with a four-part version until that migration is implemented and tested, or explicitly document a manual update for those legacy clients. Build metadata normalizes three-part versions to a fourth component of zero; four-part versions retain their revision.


Before publishing a graphics update, run `python -B tests/package_startup_tests.py build/standalone/server-upload/Standalone-DLSS-Glass-Cockpit-Refresh.zip` against the signed package. This extracts only its graphics DLLs into disposable build fixtures and checks native/bootstrap startup with both normal and copied swapchain tables. Also use `tests/official_package_tests.py OLD_ZIP NEW_FEED NEW_ZIP [OLD_AppUpdate.cs]`; the optional historical updater source verifies compatibility with the updater already in users' hands.
