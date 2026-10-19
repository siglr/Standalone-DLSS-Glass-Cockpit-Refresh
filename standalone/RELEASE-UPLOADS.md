# Publish v1.0.2.10

Staging folder: `C:\Standalone-DLSS-Glass-Cockpit-Refresh\build\standalone\server-upload`.

## GitHub

Repository: siglr/Standalone-DLSS-Glass-Cockpit-Refresh. Release/tag: `v1.0.2.10`. Use RELEASE-NOTES.md as the release description. Commit/push the corresponding source before creating the tag. Attach the final ZIP, SHA-256, fixed-name hashes, signed version JSON and fixed-name source ZIP/checksum. Also retain legacy/bridge archives, feeds and matching source as compatibility assets; identify them as automatic-update intermediates, not normal user downloads.

## WSG / cPanel

Destination: `/otherdata/tools/`. Follow PUBLISHING.md's ordered steps: source and compatibility files first, compatible .htaccess routing next, final ZIP/HTML/hashes next, signed main version JSON last. Verify live routes and checksums afterward. Preserve other tools' files and newer live .htaccess rules.

The staged index.html is copied unchanged from `C:\WSG MSFS 2024 FG Switcher\dist\website\index.html`. It includes OpenXR FG Switcher and matches the currently live index. Never substitute the older Stream Feeder copy or historical redirect. It references existing shared assets; those other tools/assets are not replaced by this release.

## Flightsim.to and HangarAddons

Update the existing product entries to version `1.0.2.10`. Upload only `Standalone-DLSS-Glass-Cockpit-Refresh.zip` as the normal download. Use RELEASE-NOTES.md for the changelog. Keep corresponding source available via the GitHub release and WSG download page. The binary ZIP also contains the official engine's corresponding source.

Suggested current description:

> A small independent installer and settings app by siglr / WeSimGlide for IFreemz's official standalone Glass Cockpit DLSS Fix. Improve glass-cockpit clarity with MSFS 2024 DLSS/FSR upscaling on desktop or in VR, without installing full ReShade. The official DLL stays unchanged. Installation defaults to the latest stable release and verifies the author's release-note SHA-256; a version selector allows rollback. Existing settings and OFXR configuration are preserved. OFXR remains separate for frame generation. Installer support goes to siglr / WSG; official-DLL screen issues go to IFreemz. Do not combine it with the ReShade add-on or another copy of the fix. Known optional v1.0.1 Dark screen fix ON glare issue: the author's default remains OFF.

Existing users: open the app normally; compatibility updates can reopen it more than once. Then close MSFS and click Update if the installed DLL needs changing. No uninstall is required. Do not tell users to disable antivirus protection or claim antivirus clearance.
