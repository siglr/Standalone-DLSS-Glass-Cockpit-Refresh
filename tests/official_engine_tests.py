"""Version selection, archive validation, migration/rollback and actual new UI. No MSFS writes."""
from pathlib import Path
import configparser,copy,hashlib,io,json,re,shutil,subprocess,tempfile,zipfile
from signed_fixture import ROOT,Signing,compile_cs
from fixture_cleanup import cleanup_fixture

base=ROOT/'build/official-engine'
work=Path(tempfile.mkdtemp(prefix='standalone-tests-official-',dir=ROOT/'build')).resolve()
runner=work/'runner.exe'
compile_cs(runner,ROOT/'tests/official_engine_runner.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',base/'signing/TestPublicKey.cs',base/'ProductVersion.cs')
count=0
def run(*args,code=0):
    global count
    result=subprocess.run([str(runner),*map(str,args)],capture_output=True,text=True,timeout=30)
    assert result.returncode==code,(args,result.returncode,result.stdout,result.stderr)
    count+=1;return result
def write(name,obj):
    p=work/name;p.write_text(json.dumps(obj));return p
def sha(data):return hashlib.sha256(data).hexdigest()
def notes(zip_hash,dll_hash):return f'- `glass-cockpit-dlss-fix-standalone.zip`: `{zip_hash}` (its `dxgi.dll`: `{dll_hash}`)\n'
def snapshot(folder):return {p.relative_to(folder).as_posix():p.read_bytes() for p in folder.rglob('*') if p.is_file()}
try:
    source=(base/'upstream-source/src/glass_cockpit_dlss_fix.cpp').read_text(encoding='utf-8')
    loader=source[source.index('void load_settings() {'):source.index('void save_settings() {')]
    defaults=configparser.ConfigParser();defaults.read(ROOT/'standalone/glass-cockpit-dlss-fix.ini')
    for section in defaults.sections():
        for key,value in defaults.items(section):
            match=re.search(r'GetPrivateProfileIntW\(L"'+re.escape(section)+r'", L"'+re.escape(key)+r'", (\w+),',loader)
            assert match,(section,key)
            default=match[1]
            if not default.isdigit():default=re.search(r'\b'+default+r'\s*=\s*(\d+)',source)[1]
            assert int(value)==int(default),(section,key,value,default)
    lock=json.loads((ROOT/'standalone/official-engine-lock.json').read_text())
    real={'id':lock['releaseId'],'tag_name':lock['release'],'body':notes(lock['zipSha256'],lock['dllSha256']),'draft':False,'prerelease':False,'assets':[
        {'name':lock['asset'],'digest':'sha256:'+lock['zipSha256'],'size':(base/'official.zip').stat().st_size,
         'browser_download_url':f"https://github.com/{lock['repository']}/releases/download/{lock['release']}/{lock['asset']}"}]}
    choices=[]
    for n in range(15):
        release=copy.deepcopy(real);release['id']=n+1;release['tag_name']=f'v1.0.{n}'
        for a in release['assets']:a['browser_download_url']=f'https://github.com/IFreemz/msfs2024-glass-cockpit-dlss-fix/releases/download/v1.0.{n}/'+a['name']
        choices.append(release)
    rows=run('list',write('versions.json',choices)).stdout.splitlines()
    assert len(rows)==10 and rows[0].endswith('v1.0.14') and rows[-1].endswith('v1.0.5'),rows
    for modify in (lambda r:r.update(draft=True),lambda r:r.update(prerelease=True),lambda r:r.update(tag_name='../../escape'),lambda r:r.update(assets=[]),
                   lambda r:r['assets'][0].update(browser_download_url='https://evil.invalid/engine.zip'),lambda r:r['assets'][0].update(digest=None),
                   lambda r:r.update(tag_name='v99999.0.0'),lambda r:r.update(tag_name='v01.0.1'),lambda r:r.update(assets=[None])):
        invalid=copy.deepcopy(real);modify(invalid)
        assert not run('list',write('invalid.json',[invalid])).stdout.strip()
        assert len(run('list',write('mixed.json',[invalid,real])).stdout.splitlines())==1
    metadata=write('release.json',real)
    run('latest',metadata)
    for body in (None,'','- `glass-cockpit-dlss-fix.addon64`: `'+lock['zipSha256']+'`',
                 notes('0'*64,lock['dllSha256']),real['body']*2):
        invalid=copy.deepcopy(real);invalid['body']=body
        badmeta=write('bad-notes.json',invalid)
        run('latest',badmeta,code=1)
        assert not run('list',write('bad-notes-list.json',[invalid])).stdout.strip()
    separate=copy.deepcopy(real)
    separate['body']=f"- `{lock['asset']}`: `{lock['zipSha256']}`\n- `dxgi.dll` (inside the zip): `{lock['dllSha256']}`\n"
    run('archive',write('separate-hashes.json',separate),base/'official.zip')
    wrong_dll=copy.deepcopy(real);wrong_dll['body']=notes(lock['zipSha256'],'0'*64)
    run('archive',write('wrong-dll-hash.json',wrong_dll),base/'official.zip',code=1)
    assert run('archive',metadata,base/'official.zip').stdout.strip()=='06f11b0abeb4d3a9b1a14b7ec22faf6de214c8f218353f1e062471015db2a685'
    badzip=work/'bad.zip';badzip.write_bytes((base/'official.zip').read_bytes()+b'changed');run('archive',metadata,badzip,code=1)
    for layout in ('traversal','duplicate','no-license','not-dll','wrong-machine'):
        with zipfile.ZipFile(base/'official.zip') as archive:files={n:archive.read(n) for n in archive.namelist()}
        if layout=='traversal':files['../escape']=b'x'
        if layout=='no-license':del files['LICENSE']
        if layout=='not-dll':files['dxgi.dll']=b'not executable'
        if layout=='wrong-machine':
            dll=bytearray(files['dxgi.dll']);at=int.from_bytes(dll[60:64],'little');dll[at+4:at+6]=b'\x4c\x01';files['dxgi.dll']=dll
        with zipfile.ZipFile(badzip,'w',zipfile.ZIP_DEFLATED) as archive:
            for n,data in files.items():archive.writestr(n,data)
            if layout=='duplicate':archive.writestr('dxgi.dll',files['dxgi.dll'])
        altered=copy.deepcopy(real);altered['assets'][0]['digest']='sha256:'+sha(badzip.read_bytes());altered['assets'][0]['size']=badzip.stat().st_size
        altered['body']=notes(sha(badzip.read_bytes()),sha(files['dxgi.dll']))
        run('archive',write('badmeta.json',altered),badzip,code=1)
    package=work/'new'
    with zipfile.ZipFile(base/'Standalone-DLSS-Glass-Cockpit-Refresh-1.0.2.10-official-DLL-test.zip') as archive:
        assert not any(n.endswith(('.dpapi','.key','.pfx','.pem')) for n in archive.namelist())
        assert {'licenses/IFreemz/LICENSE','licenses/IFreemz/NOTICE.md','IFreemz-standalone-v1.0.1-source.zip'}<=set(archive.namelist())
        archive.extractall(package)
    game=work/'game';game.mkdir();(game/'FlightSimulator2024.exe').write_bytes(b'INERT MARKER');(game/'ofxr_bridge.ini').write_bytes(b'OFXR settings unchanged\x00\xff')
    baseline=snapshot(game)
    fresh=work/'fresh-selected';fresh.mkdir();(fresh/'FlightSimulator2024.exe').write_bytes(b'INERT MARKER')
    run('engine',metadata,base/'official.zip',package,fresh)
    assert b'=' not in (fresh/'glass-cockpit-dlss-fix.ini').read_bytes(), 'Fresh downloads must inherit the selected DLL defaults'
    run('uninstall',package,fresh)
    for name,data in [('glass-cockpit-dlss-fix.addon64',b'INERT'),('renamed.addon64',b'INERT'),
                      ('ReShade.ini',b'[GENERAL]\n'),('custom-loader.ini',b'[ADDON]\nAddonPath=D:\\other-addons\n'),
                      ('addons/renamed.addon64',b'INERT'),('dxgi.dll',b'Unknown other proxy')]:
        conflict=game/name;conflict.parent.mkdir(exist_ok=True);conflict.write_bytes(data)
        before=snapshot(game);run('install',package,game,'replace',code=1);assert snapshot(game)==before
        conflict.unlink()
        if conflict.parent!=game:conflict.parent.rmdir()
    run('install',package,game)
    conflict=game/'glass-cockpit-dlss-fix.addon64';conflict.write_bytes(b'INERT conflict after installation')
    before=snapshot(game);run('install',package,game,'replace',code=1);assert snapshot(game)==before
    run('uninstall',package,game);assert conflict.read_bytes()==b'INERT conflict after installation'
    conflict.unlink();assert snapshot(game)==baseline
    run('install',package,game)
    assert not (game/'StandaloneGlassGraphics.dll').exists()
    ini=game/'glass-cockpit-dlss-fix.ini';ini.write_text(ini.read_text()+'\n[hotkeys]\nsettings_window=Ctrl+Alt+F6\n[unknown]\nfuture=37\n')
    saved=ini.read_bytes()
    run('engine',metadata,base/'official.zip',package,game)
    receipt=json.loads((game/'.StandaloneGlass-install.json').read_text());assert receipt['engineReleaseId']==real['id']
    assert ini.read_bytes()==saved
    run('install',package,game) # Roll back from selected engine to bundled version.
    assert ini.read_bytes()==saved and json.loads((game/'.StandaloneGlass-install.json').read_text())['engineReleaseId']==0
    run('uninstall',package,game);assert snapshot(game)==baseline
    # Signed legacy package -> official DLL migration with original files backed up.
    legacy=work/'old';legacy.mkdir();signing=Signing(base/'signing')
    for n in ('Standalone-DLSS-Glass-Cockpit-Refresh.exe','Install.ps1','InstallCore.ps1','Update.ps1','Resolve-MSFS.ps1'):
        shutil.copy2(package/n,legacy/n)
    for n in ('dxgi.dll','StandaloneGlassGraphics.dll'):(legacy/n).write_bytes(('INERT LEGACY '+n).encode())
    (legacy/'StandaloneGlass.ini').write_bytes((ROOT/'standalone/StandaloneGlass.ini').read_bytes())
    (legacy/'VERSION').write_text('1.0.2')
    payload=['dxgi.dll','StandaloneGlassGraphics.dll','StandaloneGlass.ini']
    (legacy/'payload.json').write_text(json.dumps({'version':'1.0.2','files':{n:sha((legacy/n).read_bytes()) for n in payload}}))
    inventory={'schema':1,'product':'Standalone-DLSS-Glass-Cockpit-Refresh','version':'1.0.2','files':{p.name:sha(p.read_bytes()) for p in legacy.iterdir()}}
    (legacy/'app-files.json').write_text(json.dumps(signing.sign(inventory)))
    (game/'dxgi.dll').write_bytes(b'Original other proxy');original=snapshot(game)
    run('install',legacy,game,'replace')
    legacy_ini=b'; preserve comments\r\n[cockpit]\r\nenabled=0\r\nline_smoothing=23\r\nglass_strength=74\r\n[experimental]\r\ncalm_glass=1\r\n[unknown]\r\nx=9\r\n'
    (game/'StandaloneGlass.ini').write_bytes(legacy_ini)
    before=snapshot(game)
    for boundary in range(1,6):
        run('fail-'+str(boundary),package,game,code=1);assert snapshot(game)==before
    run('crash',package,game,code=71);run('recover',game);assert snapshot(game)==before
    run('install',package,game)
    migrated=ini.read_text();assert '[overlay]' in migrated and '[advanced]\nglass_strength=74' in migrated and 'line_smoothing=23' in migrated and '[unknown]\nx=9' in migrated
    assert not (game/'StandaloneGlassGraphics.dll').exists() and not (game/'StandaloneGlass.ini').exists()
    assert (package/'StandaloneGlass.saved.ini').read_bytes()==legacy_ini
    assert (game/'.StandaloneGlass-backup/dxgi.dll').read_bytes()==b'Original other proxy'
    run('uninstall',package,game);assert snapshot(game)==original
    # Retired legacy originals and an existing official INI all restore to their owners.
    (game/'StandaloneGlassGraphics.dll').write_bytes(b'Preexisting legacy graphics')
    (game/'StandaloneGlass.ini').write_bytes(b'[unknown]\noriginal=1\n')
    (game/'glass-cockpit-dlss-fix.ini').write_bytes(b'[overlay]\nline_smoothing=19\n[hotkeys]\nsettings_window=Ctrl+Alt+F6\n')
    originals=snapshot(game)
    run('install',legacy,game,'replace')
    before=snapshot(game);run('install',package,game,code=1);assert snapshot(game)==before
    run('install',package,game,'replace')
    assert (game/'StandaloneGlassGraphics.dll').read_bytes()==originals['StandaloneGlassGraphics.dll']
    assert ini.read_bytes()==originals['glass-cockpit-dlss-fix.ini']
    run('uninstall',package,game);assert snapshot(game)==originals
    for name in ('StandaloneGlassGraphics.dll','StandaloneGlass.ini','glass-cockpit-dlss-fix.ini'):(game/name).unlink()
    (game/'dxgi.dll').unlink();run('install',package,game)
    (game/'CONTROL-TEST-ONLY').write_text('offline fixture')
    ui = subprocess.run([str(package/'Standalone-DLSS-Glass-Cockpit-Refresh.exe'),'--self-test',str(game),str(base/'window.png')],capture_output=True,text=True,timeout=45)
    assert ui.returncode == 0, ui.stdout + ui.stderr
    assert (game/'ofxr_bridge.ini').read_bytes()==baseline['ofxr_bridge.ini']
    print(f'PASS {count} official-engine selection/archive/install/migration/rollback operations and new UI self-test')
finally:cleanup_fixture(work)
