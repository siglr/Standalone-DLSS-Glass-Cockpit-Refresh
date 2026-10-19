"""Assemble a signed standalone package; private signing keys are never included."""
from pathlib import Path
import hashlib,json,shutil,zipfile,sys,subprocess
development="--development" in sys.argv
root=Path(__file__).resolve().parents[1]
if (root/'standalone/UpstreamRelease.cs').exists():
 subprocess.run([sys.executable,'-B',str(root/'scripts/official_test_package.py')] if development else [sys.executable,'-B',str(root/'scripts/official_release_package.py'),*sys.argv[1:]],cwd=root,check=True)
 sys.exit(0)
sys.path.insert(0,str(root/'scripts'))
from release_signing import prepare,sign
public_key=prepare()
build=root/'build/standalone';out=build/'package';out.mkdir(parents=True,exist_ok=True)
version=(root/'standalone/VERSION').read_text().strip()
cache=(build/'CMakeCache.txt').read_text()
assert ('GLASS_ENABLE_OFFICIAL_UPDATES:BOOL=OFF' in cache) if development else ('GLASS_ENABLE_OFFICIAL_UPDATES:BOOL=ON' in cache), 'Choose development packaging for source builds; official publishing requires explicit opt-in'
assert (build/'control/VERSION').read_text().strip()==version, 'Rebuild the standalone window'
assert public_key.encode('utf-16-le') in (build/'control/Standalone-DLSS-Glass-Cockpit-Refresh.exe').read_bytes(), 'Launcher does not contain the release signing public key'
items={
 'Standalone-DLSS-Glass-Cockpit-Refresh.exe':build/'control/Standalone-DLSS-Glass-Cockpit-Refresh.exe',
 'dxgi.dll':build/'Release/dxgi.dll', 'StandaloneGlassGraphics.dll':build/'Release/StandaloneGlassGraphics.dll',
 'StandaloneGlass.ini':root/'standalone/StandaloneGlass.ini',
 'Install.ps1':root/'standalone/LegacyInstallerNotice.ps1','InstallCore.ps1':root/'standalone/LegacyInstallerNotice.ps1',
 'Update.ps1':root/'standalone/LegacyInstallerNotice.ps1',
'Resolve-MSFS.ps1':root/'standalone/LegacyInstallerNotice.ps1','VERSION':root/'standalone/VERSION',
 'LICENSE.md':root/'LICENSE.md','NOTICE.md':root/'NOTICE.md','ENGINE-LICENSE.md':root/'ENGINE-LICENSE.md',
 'README.md':root/'standalone/README.md','TESTING.md':root/'standalone/TESTING.md','VALIDATION.md':root/'standalone/VALIDATION.md',
 'SECURITY.md':root/'standalone/SECURITY.md',
 'upstream-lock.json':root/'standalone/upstream-lock.json',
 'UPSTREAM.md':root/'standalone/UPSTREAM.md'}
for vendor in ['glass','reshade']:
 for f in (root/'vendor'/vendor).rglob('*'):
  if f.is_file() and f.name.upper().startswith(('LICENSE','COPYING','THIRD_PARTY','NOTICE')) and not any(p in ['.git','build','examples'] for p in f.parts):
   items[(Path('licenses')/vendor/f.relative_to(root/'vendor'/vendor)).as_posix()]=f
for name,src in items.items():
 assert src.is_file(),str(src)
 dest=out/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(src,dest)
names=['dxgi.dll','StandaloneGlassGraphics.dll','StandaloneGlass.ini']
(out/'payload.json').write_text(json.dumps({'version':version,'product':'Standalone-DLSS-Glass-Cockpit-Refresh','files':{n:hashlib.sha256((out/n).read_bytes()).hexdigest() for n in names}},indent=2)+'\n')
managed=[*items,'payload.json']
(out/'app-files.json').write_text(json.dumps({'schema':1,'product':'Standalone-DLSS-Glass-Cockpit-Refresh','version':version,'files':{n:hashlib.sha256((out/n).read_bytes()).hexdigest() for n in managed}},indent=2)+'\n')
sign(out/'app-files.json')
archive=root/'build'/('Standalone-DLSS-Glass-Cockpit-Refresh-development.zip' if development else 'Standalone-DLSS-Glass-Cockpit-Refresh.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
 for name in [*managed,'app-files.json']:z.write(out/name,name)
print(archive)
if development:
 print("Development package only; no official update feed generated.")
 sys.exit(0)

# Ready-to-upload pair; the version file is published LAST on the server.
server=build/'server-upload';server.mkdir(exist_ok=True)
shutil.copy2(archive,server/archive.name)
# Supply the exact corresponding source, including local engine changes and build scripts.
# Git's ignore rules exclude build products, private keys and personal settings.
source_names=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=root).decode('utf-8').split('\0')
source_names=sorted({n for n in source_names if n and (root/n).is_file()})
assert all(not n.lower().endswith(('.dpapi','.pfx','.pem','.key')) for n in source_names), 'Private-key material must never enter source archives'
source=server/('Standalone-DLSS-Glass-Cockpit-Refresh-'+version+'-source.zip')
with zipfile.ZipFile(source,'w',zipfile.ZIP_DEFLATED) as z:
 for name in source_names:z.write(root/name,name)
 z.writestr('SOURCE-SNAPSHOT.json',json.dumps({'version':version,'baseCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),'files':{n:hashlib.sha256((root/n).read_bytes()).hexdigest() for n in source_names}},indent=2)+'\n')
(source.with_suffix(source.suffix+'.sha256')).write_text(hashlib.sha256(source.read_bytes()).hexdigest()+'  '+source.name+'\n',encoding='utf-8')

feed={'schema':1,'product':'Standalone-DLSS-Glass-Cockpit-Refresh','version':version,'sha256':hashlib.sha256(archive.read_bytes()).hexdigest(),'size':archive.stat().st_size,'source':source.name,'sourceSha256':hashlib.sha256(source.read_bytes()).hexdigest()}
(server/'Standalone-DLSS-Glass-Cockpit-Refresh.version.json').write_text(json.dumps(feed,indent=2)+'\n')
sign(server/'Standalone-DLSS-Glass-Cockpit-Refresh.version.json')
details={'version':version,'verification':'SHA-256 identifies files; a matching hash or a scan is not a safety certification. Release signatures are separate from Windows Authenticode signing.','files':{archive.name:feed['sha256'],**{n:hashlib.sha256((out/n).read_bytes()).hexdigest() for n in ['Standalone-DLSS-Glass-Cockpit-Refresh.exe','dxgi.dll','StandaloneGlassGraphics.dll']}},'antivirusStatus':'Not independently cleared. Vendor review is separate; no automatic upload or scan was performed.'}
details['scanLinks']={n:'https://www.virustotal.com/gui/file/'+h+'/detection' for n,h in details['files'].items()}
(server/('Standalone-DLSS-Glass-Cockpit-Refresh-'+version+'.hashes.json')).write_text(json.dumps(details,indent=2)+'\n')
shutil.copy2(root/'standalone/PUBLISHING.md',server/'PUBLISHING.md')
(server/(archive.name+'.sha256')).write_text(feed['sha256']+'  '+archive.name+'\n',encoding='utf-8')
# The website is published alongside the archive, not installed with the app.
shutil.copy2(root/'standalone/site/Standalone-DLSS-Glass-Cockpit-Refresh.html',server/'Standalone-DLSS-Glass-Cockpit-Refresh.html')
shutil.copy2(root/'standalone/site/cache-headers.conf',server/'cache-headers.conf')
print(server)
