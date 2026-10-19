"""Real signed releases through historical updaters, then official-DLL installation.

Only disposable files; never runs simulator code or contacts the update service.
"""
from pathlib import Path
import hashlib,json,re,subprocess,tempfile,urllib.request,zipfile
from signed_fixture import ROOT,compile_cs
from fixture_cleanup import cleanup_fixture

PRODUCT='Standalone-DLSS-Glass-Cockpit-Refresh'
OUT=ROOT/'build/standalone/server-upload'
PREP=ROOT/'build/release-preparation'
VERSION=(ROOT/'standalone/VERSION').read_text().strip()
work=Path(tempfile.mkdtemp(prefix='standalone-tests-release-migration-',dir=ROOT/'build')).resolve()
sha=lambda b:hashlib.sha256(b).hexdigest()
def call(exe,*args,code=0):
    r=subprocess.run([str(exe),*map(str,args)],capture_output=True,text=True,timeout=40)
    assert r.returncode==code,(args,r.returncode,r.stdout,r.stderr)
    return r.stdout
def unzip(path,dest):
    with zipfile.ZipFile(path) as z:z.extractall(dest)
def source(tag):
    dest=work/tag;dest.mkdir()
    name=PRODUCT+('-source.zip' if tag=='v1.0.2.10' else '-'+tag[1:]+'-source.zip')
    with zipfile.ZipFile(PREP/tag/name) as z:
        for n in ('standalone/AppUpdate.cs','standalone/ReleaseTrust.cs'):(dest/Path(n).name).write_bytes(z.read(n))
    return dest
def runner(folder,app_source,trust,version):
    vs=folder/'Version.cs'
    vs.write_text('internal static class ProductVersion { internal const string Value="'+version+'"; }')
    exe=folder/'update.exe'
    compile_cs(exe,ROOT/'tests/app_update_runner.cs',app_source,trust,ROOT/'standalone/ReleasePublicKey.cs',vs)
    return exe
def step(exe,app,prefix,expected):
    folder=OUT if prefix==PRODUCT else OUT/'compatibility'
    feed=folder/(prefix+'.version.json');archive=folder/(prefix+'.zip')
    call(exe,'prepare',app,feed,archive);call(exe,'apply',app)
    call(exe,'verify',app,feed)
    assert (app/'VERSION').read_text().strip()==expected
    assert not (app/'.app-update').exists()
    inventory=json.loads((app/'app-files.json').read_text())
    assert all(sha((app/n).read_bytes())==h for n,h in inventory['files'].items())

try:
    for tag,names in [('v0.1.9',[PRODUCT+'.zip']),('v1.0.1',[PRODUCT+'.zip',PRODUCT+'-1.0.1-source.zip']),
        ('v1.0.2',[PRODUCT+'.zip',PRODUCT+'-1.0.2-source.zip']),
        ('v1.0.2.10',[PRODUCT+'.zip',PRODUCT+'-source.zip'])]:
        for name in names:
            dest=PREP/tag/name
            if not dest.exists():
                dest.parent.mkdir(parents=True,exist_ok=True)
                request=urllib.request.Request('https://github.com/siglr/'+PRODUCT+'/releases/download/'+tag+'/'+name,headers={'User-Agent':'StandaloneGlass-release-validation'})
                dest.write_bytes(urllib.request.urlopen(request,timeout=30).read())
    v101=source('v1.0.1');v102=source('v1.0.2')
    old101=runner(v101,v101/'AppUpdate.cs',v101/'ReleaseTrust.cs','1.0.1')
    old102=runner(v102,v102/'AppUpdate.cs',v102/'ReleaseTrust.cs','1.0.2')
    v019=work/'v0.1.9';v019.mkdir()
    for name in ('AppUpdate.cs','ReleaseTrust.cs'):
        (v019/name).write_bytes(subprocess.check_output(['git','show','e850f96:standalone/'+name],cwd=ROOT))
    old019=runner(v019,v019/'AppUpdate.cs',v019/'ReleaseTrust.cs','0.1.9')
    bridge=work/'bridge';bridge.mkdir()
    with zipfile.ZipFile(OUT/'compatibility'/(PRODUCT+'-bridge-source.zip')) as z:
        for name in ('AppUpdate.cs','ReleaseTrust.cs'):(bridge/name).write_bytes(z.read('standalone/'+name))
    bridge_runner=runner(bridge,bridge/'AppUpdate.cs',bridge/'ReleaseTrust.cs','1.0.2.1')
    v10210=source('v1.0.2.10');old10210=runner(v10210,v10210/'AppUpdate.cs',v10210/'ReleaseTrust.cs','1.0.2.10')
    current=work/'current';current.mkdir()
    current_runner=runner(current,ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',VERSION)
    # Reproduce the legacy format restriction; the unchanged v1.0.2 updater must refuse the final ZIP.
    app=work/'rejected';unzip(PREP/'v1.0.2'/(PRODUCT+'.zip'),app)
    call(old102,'prepare',app,OUT/(PRODUCT+'.version.json'),OUT/(PRODUCT+'.zip'),code=1)
    assert (app/'VERSION').read_text().strip()=='1.0.2'
    for start in ('0.1.9','1.0.1','1.0.2','1.0.2.10'):
        app=work/('app-'+start);unzip(PREP/('v'+start)/(PRODUCT+'.zip'),app)
        personal={'Appearance.txt':b'light','WindowPlacement.json':b'{"X":200,"Y":100,"Width":660,"Height":680}',
            'SimulatorDirectory.txt':b'fixture directory','StandaloneGlass.saved.ini':b'user backup',
            'ofxr_bridge.ini':b'LowerVRAM=1\nMultiplier=2\n','Navigation.txt':b'Settings'}
        for n,b in personal.items():(app/n).write_bytes(b)
        if start=='1.0.2.10':step(old10210,app,PRODUCT,VERSION)
        else:
            if start!='1.0.2':step(old019 if start=='0.1.9' else old101,app,PRODUCT+'-legacy','1.0.2')
            step(old102,app,PRODUCT+'-bridge','1.0.2.1')
            step(bridge_runner,app,PRODUCT,VERSION)
        assert all((app/n).read_bytes()==b for n,b in personal.items())
        assert not (app/'StandaloneGlassGraphics.dll').exists()
        call(current_runner,'verify',app,OUT/(PRODUCT+'.version.json'))
        print('PASS signed automatic upgrade chain from '+start,flush=True)
    # Install old custom payload, migrate to official DLL, preserve settings/OFXR and uninstall backups.
    install=work/'install.exe'
    compile_cs(install,ROOT/'tests/native_installer_runner.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/SimulatorDiscovery.cs',
        ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs',current/'Version.cs')
    oldapp=work/'old-install';unzip(PREP/'v1.0.2'/(PRODUCT+'.zip'),oldapp)
    newapp=work/'new-install';unzip(OUT/(PRODUCT+'.zip'),newapp)
    game=work/'inert-game';game.mkdir()
    original={'FlightSimulator2024.exe':b'INERT MARKER DO NOT EXECUTE','dxgi.dll':b'Original proxy',
        'ofxr_bridge.ini':b'LowerVRAM=1\nMultiplier=2\n'}
    for n,b in original.items():(game/n).write_bytes(b)
    call(install,'install',oldapp,game,'replace')
    settings=b'[cockpit]\nenabled=1\nline_smoothing=23\n[experimental]\nseam_fix=1\n[unknown]\npreserve=42\n'
    (game/'StandaloneGlass.ini').write_bytes(settings)
    call(install,'install',newapp,game)
    converted=(game/'glass-cockpit-dlss-fix.ini').read_text()
    assert 'line_smoothing=23' in converted and '[overlay]' in converted and 'preserve=42' in converted
    assert (newapp/'StandaloneGlass.saved.ini').read_bytes()==settings
    assert not (game/'StandaloneGlassGraphics.dll').exists()
    assert (game/'dxgi.dll').read_bytes()==(newapp/'dxgi.dll').read_bytes()
    assert (game/'ofxr_bridge.ini').read_bytes()==original['ofxr_bridge.ini']
    call(install,'uninstall',newapp,game)
    assert {p.name:p.read_bytes() for p in game.iterdir() if p.is_file()}==original
    print('PASS real production package migration, settings, OFXR and original proxy restoration',flush=True)
    # Exercise the exact production EXE, not only a source-key test build.
    (game/'dxgi.dll').unlink()
    call(install,'install',newapp,game)
    (game/'CONTROL-TEST-ONLY').write_text('isolated production UI check')
    call(newapp/(PRODUCT+'.exe'),'--self-test',game,work/'production-window.png')
    print('PASS exact production EXE UI self-test',flush=True)
    # Both verified official modes must be usable in the production signed package.
    mode_runner=work/'mode.exe'
    compile_cs(mode_runner,ROOT/'tests/official_engine_runner.cs',ROOT/'standalone/NativeInstaller.cs',
        ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs',current/'Version.cs')
    saved=(game/'glass-cockpit-dlss-fix.ini').read_bytes()
    call(mode_runner,'install',newapp,game,'addon')
    assert not (game/'dxgi.dll').exists() and (game/'glass-cockpit-dlss-fix.addon64').exists()
    assert (game/'glass-cockpit-dlss-fix.ini').read_bytes()==saved
    call(newapp/(PRODUCT+'.exe'),'--self-test-addon',game,work/'production-addon-window.png')
    print('PASS exact production ReShade mode switch and UI self-test',flush=True)
    # Test the exact staged Apache rewrite expressions, with fixed routes and no redirects.
    ht=(OUT/'.htaccess').read_text(encoding='utf-8')
    pairs=re.findall(r'RewriteCond %\{HTTP_USER_AGENT\} (\S+)\s+RewriteRule (\S+) (\S+) \[END\]',ht)
    assert len(pairs)==2 and '[R' not in ht
    for ua,suffix in [('0.1.4','legacy'),('0.1.9','legacy'),('0.1.10','legacy'),('1.0.0','legacy'),
        ('1.0.1','legacy'),('1.0.2','bridge'),('1.0.2.0','bridge'),('1.0.2.1',None),('1.0.2.10',None),(VERSION,None),('1.0.3',None),('Mozilla/5.0',None)]:
        for ext in ('version.json','zip'):
            path=PRODUCT+'.'+ext;agent=PRODUCT+'/'+ua if ua[0].isdigit() else ua
            for condition,pattern,replacement in pairs:
                match=re.search(pattern,path)
                if re.search(condition,agent) and match:
                    path=replacement.replace('$1',match[1]);break
            assert path==('compatibility/' if suffix else '')+PRODUCT+('-'+suffix if suffix else '')+'.'+ext,(ua,path)
            assert (OUT/path).is_file(),path
    # Fixed public filenames: source links, checksums and versions must agree.
    for directory,label in ((OUT,''),(OUT/'compatibility','-legacy'),(OUT/'compatibility','-bridge')):
        manifest=json.loads((directory/(PRODUCT+label+'.version.json')).read_text())
        source_name=PRODUCT+label+'-source.zip'
        assert manifest['source']==source_name
        assert sha((directory/source_name).read_bytes())==manifest['sourceSha256']
        for name in (PRODUCT+label+'.zip',source_name):
            assert (directory/(name+'.sha256')).read_text().strip()==sha((directory/name).read_bytes())+'  '+name
    assert not any(re.search(r'-\d+\.\d+',p.name) for p in OUT.rglob('*') if p.is_file())
    assert json.loads((OUT/(PRODUCT+'.hashes.json')).read_text())['files'][PRODUCT+'.zip']==sha((OUT/(PRODUCT+'.zip')).read_bytes())
    assert b'WSG-OpenXR-FG-Switcher.html' in (OUT/'index.html').read_bytes()
    staging=json.loads((PREP/'STAGING.json').read_text())
    assert sha((OUT/'index.html').read_bytes())==staging['sharedIndexSha256']
    shared=Path(staging['sharedIndexSource'])
    if shared.is_file():
        assert sha(shared.read_bytes())==staging['sharedIndexBaselineSha256']
        strip_card=lambda text:re.sub(r'<article class="tool">(?:(?!</article>).)*'+PRODUCT+r'\.html.*?</article>','GLASS-CARD',text,flags=re.S)
        assert strip_card((OUT/'index.html').read_text())==strip_card(shared.read_text()),'Unrelated shared-index content changed'
    compatibility=json.loads((ROOT/'standalone/compatibility-lock.json').read_text(encoding='utf-8-sig'))
    assert all(sha((OUT/'compatibility'/n).read_bytes())==h for n,h in compatibility['files'].items())
    with zipfile.ZipFile(OUT/(PRODUCT+'.zip')) as z:
        assert {n for n in z.namelist() if n.endswith('.md')}=={'LICENSE.md','licenses/IFreemz/NOTICE.md'}
        assert 'README.txt' in z.namelist() and 'Source code/IFreemz-addon-v1.0.1-source.zip' in z.namelist()
        assert b'local test build' not in z.read('README.txt')
    print('PASS immutable compatibility assets, fixed filenames, routing expressions and preserved other-tool index cards; live Apache checks remain for publication',flush=True)
finally:cleanup_fixture(work)
