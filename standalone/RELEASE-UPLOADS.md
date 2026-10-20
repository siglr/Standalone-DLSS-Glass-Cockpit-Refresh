# Publish v1.0.2.16

Staging folder: `C:\Standalone-DLSS-Glass-Cockpit-Refresh\build\standalone\server-upload`.
Maintainer notes and listing material: `build/release-preparation` (do not upload that whole folder).

## GitHub

Repository: siglr/Standalone-DLSS-Glass-Cockpit-Refresh. Release/tag: `v1.0.2.16`. Commit/push the corresponding source before creating the tag. Use RELEASE-NOTES.md as the changelog and RELEASE-DESCRIPTION.md for the full product description. Attach the final ZIP, SHA-256, hashes JSON, signed version JSON and source ZIP/checksum. Published legacy/bridge assets remain immutable in release v1.0.2.10; this release reuses them rather than creating replacements.

## WSG / cPanel

Destination: `/otherdata/tools/`. Keep the existing `compatibility/` files and routing. Upload source/checksums first, current ZIP/hash/page/screenshot files next, and the signed main version JSON last. The staged compatibility folder contains byte-identical published files for verification or restoration, not new releases. There is no history folder.

The latest shared index baseline is `C:\WSG MSFS 2024 FG Switcher\dist\website\index.html`, including OpenXR FG Switcher. Only the Glass Cockpit Refresh card's description, image metadata and cache-busting query are updated in the staged index; other tool cards remain unchanged. Keep the cockpit illustration at the top of the specific mod page; the new dark Setup screenshot belongs on the shared tools index. Only this tool's screenshot assets are staged. Preserve newer live index and .htaccess changes when publishing. Do not upload or overwrite other tools' packages or assets.

Verify the normal browser download and the legacy User-Agent routes after deployment. They must return HTTP 200 directly, without redirects, and match signed hashes with no-store/Vary headers. Local checks do not establish live deployment.

## Flightsim.to and HangarAddons

Update the existing entries to version `1.0.2.16`. Upload only `Standalone-DLSS-Glass-Cockpit-Refresh.zip` as the normal download. Replace the current product description with RELEASE-DESCRIPTION.md. Use RELEASE-NOTES.md as the version changelog; it is kept under HangarAddons’ 2,000-character limit. Keep the existing four user-supplied screenshots and videos. The same gallery files are staged in assets: glass-cockpit-refresh.png, glass-cockpit-refresh-settings.png, glass-cockpit-refresh-light.png and glass-cockpit-refresh-settings-light.png. Preserve every existing video. Keep the description’s note that they show a test build; official downloads enable automatic app updates.

Keep corresponding source linked through GitHub and the WSG download page. The user ZIP also contains both official graphics source archives under Source code. ReShade itself is not included.

Existing users: open the app normally; compatibility updates may reopen it more than once. Close MSFS before changing its installed files. No uninstall is required. Installer issues go to siglr/WSG; issues reproduced with the same official graphics version and mode installed manually go to IFreemz. Do not claim antivirus clearance.
