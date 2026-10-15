"""Signed inert native installer fixtures. No real MSFS/OFXR writes or simulation launch."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,tempfile,sys
from fixture_cleanup import cleanup_fixture
from signed_fixture import Signing,compile_cs,ROOT
work=Path(tempfile.mkdtemp(prefix='standalone-tests-',dir=ROOT/'build')).resolve()
signing=Signing(work);runner=work/'native.exe'
compile_cs(runner,ROOT/'tests/native_installer_runner.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/SimulatorDiscovery.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',signing.public,ROOT/'build/standalone/control/ProductVersion.cs')
product='Standalone-DLSS-Glass-Cockpit-Refresh';exe=product+'.exe';names=['dxgi.dll','StandaloneGlassGraphics.dll','StandaloneGlass.ini']
package=work/'package';package.mkdir()
data={n:('INERT '+n).encode() for n in [*names,exe,'Install.ps1','InstallCore.ps1','Update.ps1','Resolve-MSFS.ps1','VERSION']}
for n,v in data.items():(package/n).write_bytes(v)
def sha(b):return hashlib.sha256(b).hexdigest()
def seal(version='0.1.9'):
 (package/'VERSION').write_text(version)
 (package/'payload.json').write_text(json.dumps({'schema':1,'version':version,'product':product,'files':{n:sha((package/n).read_bytes()) for n in names}}))
 inv={'schema':1,'version':version,'product':product,'files':{p.name:sha(p.read_bytes()) for p in package.iterdir() if p.name in [*data,'payload.json']}}
 (package/'app-files.json').write_text(json.dumps(signing.sign(inv)))
seal()
count=0
def run(target,mode='install',replace=False,code=0):
 global count
 r=subprocess.run([str(runner),mode,str(package),str(target)]+(['replace'] if replace else []),capture_output=True,text=True,timeout=20)
 assert r.returncode==code,(mode,r.stdout,r.stderr);count+=1;return r
def game(name):
 p=work/name;p.mkdir();(p/'FlightSimulator2024.exe').write_text('INERT MARKER - NEVER EXECUTE');return p
def snapshot(p):return {f.relative_to(p).as_posix():f.read_bytes() for f in p.rglob('*') if f.is_file() and '.StandaloneGlass-transaction' not in f.parts}
try:
 subprocess.run([sys.executable,'-B',str(ROOT/'tests/discovery_tests.py'),'--native',str(runner)],check=True)
 p=game("MSFS 2024 O'Brien");(p/'ofxr_bridge.ini').write_bytes(b'original OFXR settings');original=snapshot(p)
 run(p);run(p);(p/'StandaloneGlass.ini').write_bytes(b'first settings');run(p,'uninstall');assert snapshot(p)==original
 assert (package/'StandaloneGlass.saved.ini').read_bytes()==b'first settings'
 run(p);(p/'StandaloneGlass.ini').write_bytes(b'second settings');(p/'StandaloneGlassGraphics.dll').unlink();run(p,'uninstall');assert snapshot(p)==original
 assert (package/('StandaloneGlass.saved.'+sha(b'second settings')+'.ini')).read_bytes()==b'second settings'
 (p/'.MyOwnFGVRMod-install.json').write_text('{}');run(p,code=1);(p/'.MyOwnFGVRMod-install.json').unlink()
 (p/'dxgi.dll').write_bytes(b'original proxy');baseline=snapshot(p);run(p,code=1);run(p,replace=True)
 (p/'StandaloneGlassGraphics.dll').write_bytes(b'user change');before=snapshot(p);run(p,'uninstall',code=1);assert snapshot(p)==before
 (p/'StandaloneGlassGraphics.dll').write_bytes(data['StandaloneGlassGraphics.dll'])
 backup=p/'.StandaloneGlass-backup/dxgi.dll';backup.write_bytes(b'damaged');run(p,'uninstall',code=1);backup.write_bytes(b'original proxy')
 run(p,'uninstall');assert snapshot(p)==baseline
 # Fresh and installed transactions roll back failures and recover hard interruption.
 for existing in [False,True]:
  for mode in ['fail','crash','commit-crash']:
   t=game('tx-'+str(existing)+mode);(t/'dxgi.dll').write_bytes(b'old proxy')
   if existing:run(t,replace=True);(t/'StandaloneGlass.ini').write_bytes(b'keep custom settings')
   before=snapshot(t);run(t,mode,replace=True,code={'fail':1,'crash':71,'commit-crash':72}[mode])
   if mode=='fail':assert snapshot(t)==before
   else:run(t,replace=True)
   if mode!='fail':
    assert (t/'dxgi.dll').read_bytes()==data['dxgi.dll']
    if existing:assert (t/'StandaloneGlass.ini').read_bytes()==b'keep custom settings'
    run(t,'uninstall');assert (t/'dxgi.dll').read_bytes()==b'old proxy'
 # Repeated upgrade keeps settings and the original proxy backup.
 run(p,replace=True);(p/'StandaloneGlass.ini').write_bytes(b'keep my entire INI')
 (package/'dxgi.dll').write_bytes(b'INERT v2');seal('0.2.0');run(p)
 assert (p/'StandaloneGlass.ini').read_bytes()==b'keep my entire INI'
 assert (p/'.StandaloneGlass-backup/dxgi.dll').read_bytes()==b'original proxy'
 run(p,'uninstall');assert snapshot(p)==baseline
 # Fake/untrusted inventory and metadata/payload changes are rejected before writes.
 for name in ['app-files.json','payload.json','dxgi.dll',exe]:
  saved=(package/name).read_bytes();(package/name).write_bytes(b'altered');before=snapshot(p)
  run(p,replace=True,code=1);assert snapshot(p)==before;(package/name).write_bytes(saved)
 # Upgrade recovery from an actual old-format journal and receipt.
 run(p,replace=True);receipt=p/'.StandaloneGlass-install.json';oldreceipt=receipt.read_bytes();backup=p/'.StandaloneGlass-backup'
 olddll=(p/'dxgi.dll').read_bytes();(backup/'.update-old-dxgi.dll').write_bytes(olddll);(p/'dxgi.dll').write_bytes(b'interrupted old updater')
 (backup/'.update.json').write_text(json.dumps({'oldReceiptHash':sha(oldreceipt),'newReceiptHash':'0'*64,'files':{'dxgi.dll':{'oldHash':sha(olddll),'newHash':sha(b'interrupted old updater')}}}))
 run(p);assert (p/'dxgi.dll').read_bytes()==olddll;run(p,'uninstall')
 # Read-only or locked destinations must not cause partial replacement.
 run(p,replace=True)
 before=snapshot(p);run(p,'locked');assert snapshot(p)==before
 # A junction must never redirect installation into another directory.
 outside=game('outside');link=work/'linked-game'
 command="New-Item -ItemType Junction -Path '"+str(link)+"' -Target '"+str(outside)+"' | Out-Null"
 subprocess.run(['powershell.exe','-NoProfile','-NonInteractive','-Command',command],check=True,capture_output=True)
 try:
  before=snapshot(outside);run(link,code=1);assert snapshot(outside)==before
 finally:os.rmdir(link)
 # Malicious receipt cannot escape the fixed payload list.
 saved=receipt.read_bytes();bad=json.loads(saved);bad['files']['../escape.dll']='0'*64;receipt.write_text(json.dumps(bad));before=snapshot(p)
 run(p,'uninstall',code=1);assert snapshot(p)==before;receipt.write_bytes(saved);run(p,'uninstall')
 # Actual production UI uses native discovery, with inert marker files and no network.
 (p/'dxgi.dll').unlink()
 run(p);ini=(ROOT/'standalone/StandaloneGlass.ini').read_bytes();(p/'StandaloneGlass.ini').write_bytes(ini)
 (p/'CONTROL-TEST-ONLY').write_text('fixture');shutil.copy2(ROOT/'build/standalone/control'/exe,package/exe)
 subprocess.run([str(package/exe),'--self-test',str(p),str(ROOT/'build/standalone-window-preview.png')],check=True,timeout=30)
 assert (p/'StandaloneGlass.ini').read_bytes()==ini and (p/'ofxr_bridge.ini').read_bytes()==b'original OFXR settings'
 print(f'PASS: {count} native installer operations, signed payloads, rollback, interrupted/legacy recovery, settings, backups and production UI')
finally:cleanup_fixture(work)
