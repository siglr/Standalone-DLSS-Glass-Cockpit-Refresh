"""Official add-on verification and transactional mode changes in disposable game folders."""
from pathlib import Path
import copy,hashlib,json,subprocess,tempfile,zipfile
from signed_fixture import ROOT,compile_cs
from fixture_cleanup import cleanup_fixture

base=ROOT/'build/official-engine'
work=Path(tempfile.mkdtemp(prefix='standalone-tests-modes-',dir=ROOT/'build')).resolve()
count=0
def snapshot(path):return {str(p.relative_to(path)):p.read_bytes() for p in path.rglob('*') if p.is_file()}
def run(*args,code=0):
    global count
    r=subprocess.run([str(work/'runner.exe'),*map(str,args)],capture_output=True,text=True,timeout=30)
    assert r.returncode==code,(args,r.returncode,r.stdout,r.stderr)
    count+=1;return r
def save_meta(value):
    path=work/'release.json';path.write_text(json.dumps(value));return path
def game(name):
    path=work/name;path.mkdir();(path/'FlightSimulator2024.exe').write_bytes(b'INERT TEST MARKER')
    (path/'ofxr_bridge.ini').write_bytes(b'INDEPENDENT PERSONAL SETTINGS\xff\x00');return path
try:
    compile_cs(work/'runner.exe',ROOT/'tests/official_engine_runner.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',base/'signing/TestPublicKey.cs',base/'ProductVersion.cs')
    package=work/'package'
    with zipfile.ZipFile(base/'Standalone-DLSS-Glass-Cockpit-Refresh-test.zip') as z:z.extractall(package)
    addon='glass-cockpit-dlss-fix.addon64';ini='glass-cockpit-dlss-fix.ini'
    addon_bytes=(package/addon).read_bytes();dll_bytes=(package/'dxgi.dll').read_bytes()
    release=json.loads((base/'official-release.json').read_text())
    assert run('archive',save_meta(release),package/addon,'addon').stdout.strip()==hashlib.sha256(addon_bytes).hexdigest()
    for edit in ('missing-notes','wrong-note','duplicate-note','missing-digest','wrong-digest','wrong-url','prerelease','standalone-hashes-only'):
        r=copy.deepcopy(release);asset=next(a for a in r['assets'] if a['name']==addon)
        if edit=='missing-notes':r['body']=''
        if edit=='wrong-note':r['body']=r['body'].replace(hashlib.sha256(addon_bytes).hexdigest(),'0'*64)
        if edit=='duplicate-note':r['body']+='\n- `'+addon+'`: `'+hashlib.sha256(addon_bytes).hexdigest()+'`\n'
        if edit=='missing-digest':asset['digest']=None
        if edit=='wrong-digest':asset['digest']='sha256:'+'0'*64
        if edit=='wrong-url':asset['browser_download_url']='https://example.invalid/other.addon64'
        if edit=='prerelease':r['prerelease']=True
        if edit=='standalone-hashes-only':r['body']='\n'.join(line for line in r['body'].splitlines() if not line.startswith('- `'+addon+'`'))
        run('archive',save_meta(r),package/addon,'addon',code=1)
    tampered=work/'bad.addon64';tampered.write_bytes(addon_bytes+b'changed')
    run('archive',save_meta(release),tampered,'addon',code=1)
    # Hash-verified but malformed PE must still be rejected.
    tampered.write_bytes(b'not an x64 DLL');r=copy.deepcopy(release);asset=next(a for a in r['assets'] if a['name']==addon)
    digest=hashlib.sha256(tampered.read_bytes()).hexdigest();asset.update(digest='sha256:'+digest,size=tampered.stat().st_size)
    r['body']='- `'+addon+'`: `'+digest+'`\n';run('archive',save_meta(r),tampered,'addon',code=1)
    p=game('switch');original=snapshot(p)
    run('install',package,p)
    (p/ini).write_bytes(b'; personal settings\r\n[overlay]\r\nline_smoothing=29\r\n[hotkeys]\r\nsettings_window=Ctrl+5\r\n[future]\r\nunknown=keep\r\n')
    settings=(p/ini).read_bytes();before=snapshot(p)
    for boundary in (1,2,3):
        run('fail-'+str(boundary),package,p,'addon',code=1);assert snapshot(p)==before
    run('crash',package,p,'addon',code=71);run('recover',p);assert snapshot(p)==before
    run('install',package,p,'addon');assert not (p/'dxgi.dll').exists() and (p/addon).read_bytes()==addon_bytes and (p/ini).read_bytes()==settings
    (p/'CONTROL-TEST-ONLY').write_text('isolated UI fixture')
    r=subprocess.run([str(package/'Standalone-DLSS-Glass-Cockpit-Refresh.exe'),'--self-test-addon',str(p),str(base/'addon-window')],capture_output=True,text=True,timeout=45)
    assert r.returncode==0,r.stdout+r.stderr
    # Only our inert, sleeping simulator-named process is started/stopped here.
    # This exercises the real process check without a game or GPU workload.
    hold=work/'hold.cs';hold.write_text('class Inert { static void Main() { System.Threading.Thread.Sleep(120000); } }')
    compile_cs(work/'FlightSimulator2024.exe',hold)
    with subprocess.Popen([str(work/'FlightSimulator2024.exe')]) as simulated_game:
        try:
            r=subprocess.run([str(package/'Standalone-DLSS-Glass-Cockpit-Refresh.exe'),'--self-test-addon',str(p),str(base/'addon-running-window')],capture_output=True,text=True,timeout=45)
            assert r.returncode==0,r.stdout+r.stderr
            before=snapshot(p)
            run('install',package,p,'addon',code=1);run('uninstall',package,p,code=1)
            assert snapshot(p)==before
        finally:simulated_game.terminate();simulated_game.wait(timeout=10)
    (p/'CONTROL-TEST-ONLY').unlink()
    # Updates and explicit rollback of the selected add-on retain every INI byte.
    run('engine',save_meta(release),package/addon,package,p,'addon');assert (p/ini).read_bytes()==settings
    run('install',package,p,'addon');before=snapshot(p)
    for boundary in (1,2,3):
        run('fail-'+str(boundary),package,p,code=1);assert snapshot(p)==before
    run('install',package,p);assert not (p/addon).exists() and (p/'dxgi.dll').read_bytes()==dll_bytes and (p/ini).read_bytes()==settings
    run('uninstall',package,p);assert snapshot(p)==original
    assert (package/'glass-cockpit-dlss-fix.saved.ini').read_bytes()==settings
    # Adopting either manually installed official mode keeps originals dormant while
    # switched, and restores exactly that original mode on final uninstall.
    for start_addon in (False,True):
        p=game('adopt-'+str(start_addon));(p/(addon if start_addon else 'dxgi.dll')).write_bytes(addon_bytes if start_addon else dll_bytes)
        (p/ini).write_bytes(settings);original=snapshot(p)
        mode=('addon',) if start_addon else ()
        run('install',package,p,*mode,code=1);assert snapshot(p)==original
        run('install',package,p,'replace',*mode)
        run('install',package,p,*(() if start_addon else ('addon',)))
        assert not ((p/addon).exists() and (p/'dxgi.dll').exists())
        run('install',package,p,*mode)
        run('uninstall',package,p);assert snapshot(p)==original
    # ReShade's own DXGI and unrelated add-ons are byte-for-byte untouched in add-on mode.
    p=game('reshade');cs=work/'version.cs'
    cs.write_text('using System.Reflection; [assembly: AssemblyProduct("ReShade")] class Inert { static void Main() {} }')
    compile_cs(p/'dxgi.dll',cs)
    (p/'ReShade.ini').write_text('[GENERAL]\n');(p/'unrelated.addon64').write_bytes(b'UNRELATED')
    original=snapshot(p)
    run('install',package,p,'addon');before=snapshot(p)
    error=run('install',package,p,code=1).stderr;assert 'ReShade' in error and snapshot(p)==before
    (p/addon).write_bytes(addon_bytes+b'modified');modified=snapshot(p)
    run('install',package,p,'addon',code=1);run('uninstall',package,p,code=1);assert snapshot(p)==modified
    (p/addon).write_bytes(addon_bytes);run('uninstall',package,p);assert snapshot(p)==original
    # No installation while an unmanaged standalone or a second add-on is present.
    for name in ('dxgi.dll','winmm.dll','version.dll','renamed.addon64'):
        p=game('conflict-'+name);(p/name).write_bytes(dll_bytes);before=snapshot(p)
        error=run('install',package,p,'addon',code=1).stderr
        assert 'IFreemz' in error and snapshot(p)==before
        if name in ('winmm.dll','version.dll'):
            assert 'different filename' in error
            run('install',package,p,code=1);assert snapshot(p)==before
    p=game('modified-manual-addon');(p/addon).write_bytes(addon_bytes+b'modified');before=snapshot(p)
    error=run('install',package,p,'addon','replace',code=1).stderr
    assert 'does not match the verified official add-on' in error and snapshot(p)==before
    # A custom ReShade destination must never silently receive an unused root copy.
    p=game('custom');(p/'ReShade.ini').write_text('[ADDON]\nAddonPath=custom-addons\n');before=snapshot(p)
    run('install',package,p,'addon',code=1);assert snapshot(p)==before
    print(f'PASS {count} mode/download/install/update/switch/rollback/uninstall operations and add-on UI self-test')
finally:cleanup_fixture(work)
