"""Maintainer production packaging: official engine, signed app, legacy update bridges.

Requires explicit --enable-official-updates and GLASS_SIGNING_KEY. Never installs,
uploads, creates a signing key, or modifies another project's website files.
"""
from pathlib import Path
import argparse, hashlib, json, os, re, shutil, struct, subprocess, sys, zipfile
from official_test_package import prepare as upstream, fetch, ROOT, WORK, PRODUCT, DLL_HASH, LOCK
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
    inventory=folder/'app-files.json'
    previous=json.loads(inventory.read_text()) if inventory.exists() else {}
    for name,digest in previous.get('files',{}).items():
        obsolete=folder/name
        if name not in items and name not in ('VERSION','payload.json') and obsolete.is_file():
            assert obsolete.resolve().is_relative_to(folder.resolve()) and not obsolete.is_symlink()
            if sha(obsolete)==digest:obsolete.unlink()
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
    assert subprocess.check_output([str(verifier),'archive',str(WORK/'official-release.json'),str(binary/'glass-cockpit-dlss-fix.addon64'),'addon'],text=True).strip()==LOCK['addon']['sha256']
    guide=PREP/'README.txt'
    guide.write_text((ROOT/'standalone/USER-README.txt').read_text(encoding='utf-8').replace(
        'This is a local test build. Automatic app updates are disabled.',
        'The app checks for updates at launch. Older apps may reopen more than once\nto complete compatibility updates. No uninstall is required.'),encoding='utf-8')
    items={EXE:control/EXE,'dxgi.dll':binary/'dxgi.dll','glass-cockpit-dlss-fix.ini':ROOT/'standalone/glass-cockpit-dlss-fix.ini',
        'glass-cockpit-dlss-fix.addon64':binary/'glass-cockpit-dlss-fix.addon64',
        'Source code/'+engine_source.name:engine_source,
        'Source code/IFreemz-addon-v1.0.1-source.zip':WORK/'IFreemz-addon-v1.0.1-source.zip',
        'Source code/README.txt':ROOT/'standalone/SOURCE-CODE-README.txt',
        'official-engine-lock.json':ROOT/'standalone/official-engine-lock.json',
        'official-release.json':WORK/'official-release.json',
        'LICENSE.md':ROOT/'LICENSE.md','README.txt':guide,
        **{'licenses/IFreemz/'+n:binary/n for n in ('LICENSE','NOTICE.md','README.txt')},
        **{n:ROOT/'standalone/LegacyInstallerNotice.ps1' for n in ('Install.ps1','InstallCore.ps1','Update.ps1','Resolve-MSFS.ps1')}}
    archive=OUT/(PRODUCT+'.zip')
    package(BUILD/'package-official',items,version,['dxgi.dll','glass-cockpit-dlss-fix.ini'],archive)
    # Reuse byte-identical published intermediates. Never rebuild or re-sign them.
    compatibility=json.loads((ROOT/'standalone/compatibility-lock.json').read_text(encoding='utf-8-sig'))
    for name,expected in compatibility['files'].items():
        assert Path(name).name==name and name.startswith(PRODUCT+'-'),name
        target=compat/name
        fetch('https://github.com/'+compatibility['repository']+'/releases/download/'+compatibility['release']+'/'+name,target)
        assert sha(target)==expected,'Published compatibility asset changed: '+name
    update_verifier=PREP/'verify-update.exe'
    compile_cs(update_verifier,ROOT/'tests/app_update_runner.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs',vs)
    for label in ('legacy','bridge'):
        prefix=PRODUCT+'-'+label
        published_feed=compat/(prefix+'.version.json')
        manifest=json.loads(published_feed.read_text())
        assert sha(compat/(prefix+'.zip'))==manifest['sha256']
        assert manifest['source']==prefix+'-source.zip'
        assert sha(compat/manifest['source'])==manifest['sourceSha256']
        checked=PREP/(label+'-verified');unpack(compat/(prefix+'.zip'),checked)
        subprocess.run([str(update_verifier),'verify',str(checked),str(published_feed)],check=True)
    # Exact application source and bundled official corresponding source.
    files=subprocess.check_output(['git','ls-files','--cached','--others','--exclude-standard','-z'],cwd=ROOT).decode().split('\0')
    source=OUT/(PRODUCT+'-source.zip')
    sources_archive(source,version,{**{n:ROOT/n for n in files if n and (ROOT/n).is_file()},
        'official-engine-source/'+engine_source.name:engine_source,
        'official-engine-source/IFreemz-addon-v1.0.1-source.zip':WORK/'IFreemz-addon-v1.0.1-source.zip'})
    feed(archive,version,OUT/(PRODUCT+'.version.json'),source)
    details={'version':version,'verification':'SHA-256 identifies files; matching hashes and scan results do not certify safety. Release signatures are not Windows Authenticode.',
        'files':{archive.name:sha(archive),EXE:sha(control/EXE),'dxgi.dll':DLL_HASH,'glass-cockpit-dlss-fix.addon64':LOCK['addon']['sha256']},'antivirusStatus':'No vendor clearance or new scan claimed.'}
    details['scanLinks']={n:'https://www.virustotal.com/gui/file/'+h+'/detection' for n,h in details['files'].items()}
    write(OUT/(PRODUCT+'.hashes.json'),details)
    for n in ('PUBLISHING.md','RELEASE-NOTES.md','RELEASE-DESCRIPTION.md','RELEASE-VALIDATION.md','RELEASE-UPLOADS.md','UPLOAD-START-HERE.txt'):
        shutil.copy2(ROOT/'standalone'/n,PREP/n)
        if (OUT/n).is_file():(OUT/n).unlink() # Retired generated maintainer-only upload instructions.
    page=(ROOT/'standalone/site'/(PRODUCT+'.html')).read_text(encoding='utf-8')
    sizes={};hashes={}
    for name,token in (('glass-cockpit-refresh.png','APP_SCREENSHOT_HASH'),('glass-cockpit-refresh-settings.png','SETTINGS_SCREENSHOT_HASH'),
        ('glass-cockpit-refresh-light.png','APP_LIGHT_SCREENSHOT_HASH'),('glass-cockpit-refresh-settings-light.png','SETTINGS_LIGHT_SCREENSHOT_HASH')):
        source_image=ROOT/'standalone/site/assets'/name
        data=source_image.read_bytes();assert data[:8]==b'\x89PNG\r\n\x1a\n','Screenshot must be PNG'
        sizes[name]=struct.unpack('>II',data[16:24]);hashes[name]=sha(source_image)[:16]
        target=OUT/'assets'/name;target.parent.mkdir(exist_ok=True);shutil.copy2(source_image,target)
        page=page.replace(token,hashes[name])
    (OUT/(PRODUCT+'.html')).write_text(page,encoding='utf-8')
    index=(args.shared_site/'index.html').read_text(encoding='utf-8')
    cards=re.findall(r'<article class="tool">.*?</article>',index,re.S)
    matching=[card for card in cards if PRODUCT+'.html' in card];assert len(matching)==1
    old_card=matching[0];card=old_card
    width,height=sizes['glass-cockpit-refresh.png']
    card=re.sub(r'assets/glass-cockpit-refresh\.png\?v=[^"\s]+','assets/glass-cockpit-refresh.png?v='+hashes['glass-cockpit-refresh.png'],card)
    card=re.sub(r'alt="[^"]*" width="\d+" height="\d+"',f'alt="Glass Cockpit Refresh Setup page with official installation mode and version selection" width="{width}" height="{height}"',card,count=1)
    card=re.sub(r'(<h2>Glass Cockpit Refresh</h2>)<p>.*?</p>',lambda m:m[1]+'<p>Install IFreemz’s official standalone DLL or ReShade add-on. Clear Setup and Settings pages, verified downloads and rollback choices for MSFS 2024.</p>',card,count=1)
    assert card!=old_card
    (OUT/'index.html').write_text(index.replace(old_card,card),encoding='utf-8')
    ht=(args.shared_site/'.htaccess').read_text(encoding='utf-8')
    block=(ROOT/'standalone/site/legacy-updates.conf').read_text(encoding='utf-8')
    marker='# BEGIN Standalone Glass compatibility updates'
    if marker in ht:
        ht,count=re.subn(r'# BEGIN Standalone Glass compatibility updates.*?# END Standalone Glass compatibility updates\n?',lambda _:block,ht,flags=re.S)
        assert count==1,'Ambiguous compatibility routing blocks'
    else:ht+='\n'+block
    (OUT/'.htaccess').write_text(ht,encoding='utf-8')
    write(PREP/'STAGING.json',{'version':version,'sharedIndexSource':str(args.shared_site/'index.html'),
        'sharedIndexBaselineSha256':sha(args.shared_site/'index.html'),'sharedIndexSha256':sha(OUT/'index.html'),
        'indexChange':'Only Glass Cockpit Refresh card text and screenshot metadata','published':False})
    if (OUT/'STAGING.json').is_file():(OUT/'STAGING.json').unlink()
    print('Prepared production release and compatibility feeds:',OUT)

if __name__=='__main__':main()
