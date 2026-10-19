"""Maintainer production packaging: official engine, signed app, legacy update bridges.

Requires explicit --enable-official-updates and GLASS_SIGNING_KEY. Never installs,
uploads, creates a signing key, or modifies another project's website files.
"""
from pathlib import Path
import argparse, hashlib, json, os, re, shutil, subprocess, sys, zipfile
from official_test_package import prepare as upstream, fetch, ROOT, WORK, PRODUCT, DLL_HASH
from release_signing import prepare as signing, sign

EXE=PRODUCT+'.exe'
BUILD=ROOT/'build/standalone'
OUT=BUILD/'server-upload'
PREP=ROOT/'build/release-preparation'
BRIDGE='1.0.2.1'

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def write(path,obj):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text(json.dumps(obj,indent=2)+'\n',encoding='utf-8')
def version_source(path,version):
    path.parent.mkdir(parents=True,exist_ok=True)
    path.write_text((ROOT/'standalone/ProductVersion.cs.in').read_text(encoding='utf-8').replace('@PROJECT_VERSION@',version).replace('@GLASS_FILE_VERSION@',version if version.count('.')==3 else version+'.0'),encoding='utf-8')
def compile_app(out,sources):
    env=os.environ.copy();env['LIB']=''
    csc=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
    subprocess.run([str(csc),'/nologo','/codepage:65001','/target:winexe','/platform:x64','/optimize+',
        '/out:'+str(out),'/win32icon:'+str(ROOT/'standalone/assets/WeSimGlide.ico'),
        *['/resource:'+str(ROOT/'standalone/assets'/n)+','+n for n in ('WeSimGlide.ico','WeSimGlide.png')],
        *['/reference:'+r+'.dll' for r in ('System.Windows.Forms','System.Drawing','System.Web.Extensions','System.IO.Compression','System.IO.Compression.FileSystem')],*map(str,sources)],env=env,check=True)
def package(folder,items,version,payload,archive):
    folder.mkdir(parents=True,exist_ok=True)
    for name,source in items.items():
        dest=folder/name;dest.parent.mkdir(parents=True,exist_ok=True)
        if dest.resolve()!=source.resolve():shutil.copy2(source,dest)
    (folder/'VERSION').write_text(version+'\n',encoding='utf-8')
    write(folder/'payload.json',{'product':PRODUCT,'version':version,'files':{n:sha(folder/n) for n in payload}})
    names=sorted(set(items)|{'VERSION','payload.json'})
    write(folder/'app-files.json',{'schema':1,'product':PRODUCT,'version':version,'files':{n:sha(folder/n) for n in names}})
    sign(folder/'app-files.json')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED) as z:
        for n in names+['app-files.json']:z.write(folder/n,n)
    archive.with_suffix('.zip.sha256').write_text(sha(archive)+'  '+archive.name+'\n',encoding='utf-8')
def feed(archive,version,path,source=None):
    obj={'schema':1,'product':PRODUCT,'version':version,'sha256':sha(archive),'size':archive.stat().st_size}
    if source:obj.update(source=source.name,sourceSha256=sha(source))
    write(path,obj);sign(path)
def unpack(archive,dest):
    with zipfile.ZipFile(archive) as z:
        for n in z.namelist():
            p=dest/n
            assert not Path(n).is_absolute() and p.resolve().is_relative_to(dest.resolve()),n
            if not n.endswith('/'):
                p.parent.mkdir(parents=True,exist_ok=True);p.write_bytes(z.read(n))
def sources_archive(path,version,files):
    assert all(not n.lower().endswith(('.dpapi','.pfx','.p12','.pem','.key')) for n in files)
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED) as z:
        for n,p in sorted(files.items()):z.write(p,n)
        z.writestr('SOURCE-SNAPSHOT.json',json.dumps({'version':version,'baseCommit':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'files':{n:sha(p) for n,p in files.items()}},indent=2))
    path.with_suffix('.zip.sha256').write_text(sha(path)+'  '+path.name+'\n',encoding='utf-8')

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--enable-official-updates',action='store_true',required=True)
    parser.add_argument('--shared-site',type=Path,required=True,help='Latest shared index.html and .htaccess folder (read-only)')
    args=parser.parse_args()
    public=signing()
    assert public in (ROOT/'standalone/ReleasePublicKey.cs').read_text(), 'Signing key does not match the pinned production public key'
    version=(ROOT/'standalone/VERSION').read_text().strip()
    assert re.fullmatch(r'\d+\.\d+\.\d+\.\d+',version) and tuple(map(int,version.split('.')))>(1,0,2,1)
    assert version=='1.0.2.10', 'Before packaging a later release, reuse the published immutable 1.0.2.1 bridge instead of rebuilding it'
    assert b'WSG-OpenXR-FG-Switcher.html' in (args.shared_site/'index.html').read_bytes(), 'Use the latest shared tools index'
    binary,engine_source=upstream()
    OUT.mkdir(parents=True,exist_ok=True);PREP.mkdir(exist_ok=True)
    compat=OUT/'compatibility';compat.mkdir(exist_ok=True)
    control=BUILD/'control';control.mkdir(exist_ok=True)
    vs=control/'ProductVersion.cs';version_source(vs,version)
    names=('GlassWindow.cs','AppUpdate.cs','NativeInstaller.cs','SimulatorDiscovery.cs','ReleaseTrust.cs','WsgTheme.cs','UpstreamRelease.cs','ReleasePublicKey.cs')
    compile_app(control/EXE,[*[ROOT/'standalone'/n for n in names],ROOT/'tools/GlassSettingsPanel.cs',vs])
    (control/'VERSION').write_text(version+'\n')
    assert public.encode('utf-16-le') in (control/EXE).read_bytes()
    sys.path.insert(0,str(ROOT/'tests'))
    from signed_fixture import compile_cs
    verifier=PREP/'verify.exe'
    compile_cs(verifier,ROOT/'tests/official_engine_runner.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs',vs)
    assert subprocess.check_output([str(verifier),'archive',str(WORK/'official-release.json'),str(WORK/'official.zip')],text=True).strip()==DLL_HASH
    items={EXE:control/EXE,'dxgi.dll':binary/'dxgi.dll','glass-cockpit-dlss-fix.ini':ROOT/'standalone/glass-cockpit-dlss-fix.ini',
        engine_source.name:engine_source,'official-engine-lock.json':ROOT/'standalone/official-engine-lock.json',
        'official-release.json':WORK/'official-release.json',
        **{n:ROOT/n for n in ('LICENSE.md','NOTICE.md','ENGINE-LICENSE.md')},
        'README.md':ROOT/'standalone/RELEASE-README.md',
        **{n:ROOT/'standalone'/n for n in ('SECURITY.md','UPSTREAM.md')},
        **{'licenses/IFreemz/'+n:binary/n for n in ('LICENSE','NOTICE.md','README.txt')},
        **{n:ROOT/'standalone/LegacyInstallerNotice.ps1' for n in ('Install.ps1','InstallCore.ps1','Update.ps1','Resolve-MSFS.ps1')}}
    archive=OUT/(PRODUCT+'.zip')
    package(BUILD/'package-official',items,version,['dxgi.dll','glass-cockpit-dlss-fix.ini'],archive)
    # Retain the immutable 1.0.2 release for clients that only parse three components.
    legacy=PREP/'v1.0.2';legacy.mkdir(exist_ok=True)
    for name in (PRODUCT+'.zip',PRODUCT+'.version.json',PRODUCT+'-1.0.2-source.zip'):
        fetch('https://github.com/siglr/'+PRODUCT+'/releases/download/v1.0.2/'+name,legacy/name)
    update_verifier=PREP/'verify-update.exe'
    compile_cs(update_verifier,ROOT/'tests/app_update_runner.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs',vs)
    old=PREP/'legacy-package';unpack(legacy/(PRODUCT+'.zip'),old)
    subprocess.run([str(update_verifier),'verify',str(old),str(legacy/(PRODUCT+'.version.json'))],check=True)
    old_feed=json.loads((legacy/(PRODUCT+'.version.json')).read_text())
    assert sha(legacy/(PRODUCT+'.zip'))==old_feed['sha256']
    for ext in ('.zip','.version.json'):shutil.copy2(legacy/(PRODUCT+ext),compat/(PRODUCT+'-legacy'+ext))
    (compat/(PRODUCT+'-legacy.zip.sha256')).write_text(old_feed['sha256']+'  '+PRODUCT+'-legacy.zip\n')
    old_source=legacy/(PRODUCT+'-1.0.2-source.zip')
    assert sha(old_source)==old_feed['sourceSha256']
    legacy_source=compat/(PRODUCT+'-legacy-source.zip')
    shutil.copy2(old_source,legacy_source)
    legacy_source.with_suffix('.zip.sha256').write_text(sha(legacy_source)+'  '+legacy_source.name+'\n')
    feed(compat/(PRODUCT+'-legacy.zip'),'1.0.2',compat/(PRODUCT+'-legacy.version.json'),legacy_source)
    # A small app-only bridge reads both payload layouts. Its graphics bytes stay 1.0.2.
    bridge=PREP/'bridge-source';unpack(old_source,bridge)
    # Compile historical application code only after comparing it to the local published commit.
    for n in [*['standalone/'+n for n in names if n!='UpstreamRelease.cs'],'tools/GlassSettingsPanel.cs']:
        expected=subprocess.check_output(['git','show','f99ea4d:'+n],cwd=ROOT)
        assert (bridge/n).read_bytes().replace(b'\r\n',b'\n')==expected.replace(b'\r\n',b'\n'), 'Historical bridge source differs: '+n
    shutil.copy2(ROOT/'standalone/AppUpdate.cs',bridge/'standalone/AppUpdate.cs')
    (bridge/'standalone/VERSION').write_text(BRIDGE+'\n')
    bridge_vs=bridge/'ProductVersion.cs';version_source(bridge_vs,BRIDGE)
    bridge_exe=PREP/'bridge.exe'
    compile_app(bridge_exe,[*[bridge/'standalone'/n for n in names if n!='UpstreamRelease.cs'],bridge/'tools/GlassSettingsPanel.cs',bridge_vs])
    old_inventory=json.loads((old/'app-files.json').read_text())
    bridge_items={n:old/n for n in old_inventory['files'] if n not in ('VERSION','payload.json')};bridge_items[EXE]=bridge_exe
    bridge_zip=compat/(PRODUCT+'-bridge.zip')
    package(PREP/'bridge-package',bridge_items,BRIDGE,['dxgi.dll','StandaloneGlassGraphics.dll','StandaloneGlass.ini'],bridge_zip)
    bridge_source=compat/(PRODUCT+'-bridge-source.zip')
    sources_archive(bridge_source,BRIDGE,{p.relative_to(bridge).as_posix():p for p in bridge.rglob('*') if p.is_file() and p.name!='SOURCE-SNAPSHOT.json'})
    feed(bridge_zip,BRIDGE,compat/(PRODUCT+'-bridge.version.json'),bridge_source)
    # Exact application source and bundled official corresponding source.
    files=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=ROOT).decode().split('\0')
    source=OUT/(PRODUCT+'-source.zip')
    sources_archive(source,version,{**{n:ROOT/n for n in files if n and (ROOT/n).is_file()},'official-engine-source/'+engine_source.name:engine_source})
    feed(archive,version,OUT/(PRODUCT+'.version.json'),source)
    details={'version':version,'verification':'SHA-256 identifies files; matching hashes and scan results do not certify safety. Release signatures are not Windows Authenticode.',
        'files':{archive.name:sha(archive),EXE:sha(control/EXE),'dxgi.dll':DLL_HASH},'antivirusStatus':'No vendor clearance or new scan claimed.'}
    details['scanLinks']={n:'https://www.virustotal.com/gui/file/'+h+'/detection' for n,h in details['files'].items()}
    write(OUT/(PRODUCT+'.hashes.json'),details)
    for n in ('PUBLISHING.md','RELEASE-NOTES.md','RELEASE-VALIDATION.md','RELEASE-UPLOADS.md'):shutil.copy2(ROOT/'standalone'/n,OUT/n)
    shutil.copy2(ROOT/'standalone/site'/(PRODUCT+'.html'),OUT/(PRODUCT+'.html'))
    shutil.copy2(args.shared_site/'index.html',OUT/'index.html')
    ht=(args.shared_site/'.htaccess').read_text(encoding='utf-8')
    assert '# BEGIN Standalone Glass compatibility updates' not in ht, 'Merge an existing compatibility block explicitly'
    (OUT/'.htaccess').write_text(ht+'\n'+(ROOT/'standalone/site/legacy-updates.conf').read_text(encoding='utf-8'),encoding='utf-8')
    write(OUT/'STAGING.json',{'version':version,'sharedIndexSource':str(args.shared_site/'index.html'),'sharedIndexSha256':sha(OUT/'index.html'),'published':False})
    shutil.copy2(ROOT/'standalone/UPLOAD-START-HERE.txt',OUT/'UPLOAD-START-HERE.txt')
    print('Prepared production release and compatibility feeds:',OUT)

if __name__=='__main__':main()
