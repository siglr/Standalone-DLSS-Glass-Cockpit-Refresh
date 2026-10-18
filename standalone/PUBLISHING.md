# Packaging and signing

Prepare all future standalone releases from this repository. Commit and push the corresponding source and version changes here; do not build releases from an older parent project or exported copy.

For a local build, run `python -B scripts/dev_package.py`. It uses a separate local DPAPI-protected development key, embeds only the public half, disables official automatic updates, and generates a development ZIP. It does not install the mod or publish files. Keep the private key out of Git and release archives.

The production public verification key is intentionally public in `standalone/ReleasePublicKey.cs`. Its private counterpart is not in this repository. Publishing an official release requires that private key, `GLASS_ENABLE_OFFICIAL_UPDATES=ON`, the production public-key source, and an explicit `GLASS_SIGNING_KEY` environment variable naming the protected private-key file. Then `python -B standalone/package.py` creates the signed archive, inventory, feed and version-specific hashes under build. It does not upload them. Publish the matching source ZIP and source checksum first, then the binary ZIP, and the signed version JSON last.

Forks must use their own signing key and keep official app updates disabled unless implementing their own update channel. Do not label a fork as an official WeSimGlide release. If adding a fork channel, change the endpoint restrictions, product identity, key and branding coherently, and test upgrade/rollback against that fork's previous versions. Published releases must never be silently replaced under the same version.

The website source lives in `standalone/site/Standalone-DLSS-Glass-Cockpit-Refresh.html`. Official packaging copies it into `build/standalone/server-upload` alongside the ZIP, SHA-256 sidecar, version-specific hashes and signed version JSON. Upload the matching source archive first, then the binary ZIP, hashes and HTML, and the signed version JSON last. The page's All tools link uses the server's adjacent `index.html`; do not replace that tools index with the historical mod redirect. Website-only edits do not require an app rebuild or version change. The HTML remains outside the app ZIP.

DPAPI CurrentUser keys depend on the Windows account/profile and machine protection. Copying the encrypted file alone is not portable recovery. Keep a protected, tested account/system backup before changing Windows or release machines. Do not silently generate a replacement for an established release key. Local release signatures are not Windows Authenticode certificates or antivirus clearance.

The current shared tools index is `C:\WSG Stream Feeder\dist\upload\index.html`. Use that file as the starting point for any requested index changes. Do not use the historical redirect in the old mod repository or its upload folder. Standalone packaging leaves the shared index untouched.

Cache policy: merge `standalone/site/cache-headers.conf` into the shared `/otherdata/tools/.htaccess`, preserving existing rules and CORS settings. This snippet is also staged with the upload files; uploading it as `.conf` alone does not activate it. The combined `.htaccess` is maintained in `C:\WSG Stream Feeder\web\.htaccess` and staged in that project's `dist\upload\.htaccess`. The HTML must revalidate before reuse; the fixed ZIP and version-feed URLs use `no-store`. Styles/scripts are inline, and the page already fetches release metadata with `cache: 'no-store'`. After uploading, check the actual HTTP response headers. Previously cached pages may need one final hard refresh before the new policy takes effect. No app rebuild or new release is needed.


For v1.0.0 and later, publish the version-specific `*-source.zip` and its SHA-256 sidecar alongside the binary ZIP. This exact source snapshot includes engine changes, shaders, dependencies and build scripts; its SOURCE-SNAPSHOT.json identifies the base commit and hashes every source file. Do not distribute the GPL graphics DLL without also supplying its corresponding source by a GPL-compliant method. The engine's license is documented in ENGINE-LICENSE.md; the separate C# application retains PolyForm Noncommercial. Preserve vendor/glass/LICENSE and vendor/glass/NOTICE.md in binary and source distributions.


## Release numbering

Standalone 1.0.2 is a transition release based on upstream 1.0.1. Previously installed updaters accept exactly three components; 1.0.2 accepts three or four. Starting with the next upstream release, use `upstream-major.upstream-minor.upstream-patch.package-revision`, beginning with revision 1 (for example 1.0.2.1), and increment the last component for standalone-only changes. Numeric ordering must remain increasing; do not publish 1.0.1.1 after 1.0.2.

Before switching the live channel to four components, retain a separately named signed 1.0.2 ZIP/feed and provide a legacy-client transition route. An older client that skips 1.0.2 still rejects four-part metadata. Do not overwrite the shared feed with a four-part version until that migration is implemented and tested, or explicitly document a manual update for those legacy clients. Build metadata normalizes three-part versions to a fourth component of zero; four-part versions retain their revision.


Before publishing a graphics update, run `python -B tests/package_startup_tests.py build/standalone/server-upload/Standalone-DLSS-Glass-Cockpit-Refresh.zip` against the signed package. This extracts only its graphics DLLs into disposable build fixtures and checks native/bootstrap startup with both normal and copied swapchain tables. Also use `tests/official_package_tests.py OLD_ZIP NEW_FEED NEW_ZIP [OLD_AppUpdate.cs]`; the optional historical updater source verifies compatibility with the updater already in users' hands.
