"""Offline self-updater checks with inert DLLs; never touch MSFS or OFXR."""
from pathlib import Path
import hashlib, io, json, os, shutil, subprocess, tempfile, time, zipfile
from fixture_cleanup import cleanup_fixture

ROOT=Path(__file__).resolve().parents[1]
WORK=Path(tempfile.mkdtemp(prefix='app-update-tests-',dir=ROOT/'build'))
PRODUCT='Standalone-DLSS-Glass-Cockpit-Refresh'
EXE=PRODUCT+'.exe'
CURRENT=(ROOT/'standalone/VERSION').read_text().strip()
# Exercise the transition into four-part versions, and subsequent revisions,
# through real signed staging, rollback and helper handoff.
parts=[int(p) for p in CURRENT.split('.')]
if len(parts)==3:parts.append(1)
else:parts[-1]+=1
NEXT='.'.join(map(str,parts))
from signed_fixture import Signing,compile_cs
signing=Signing(WORK)
RUNNER=WORK/'runner.exe'
compile_cs(RUNNER,ROOT/'tests/app_update_runner.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',signing.public,ROOT/'build/standalone/control/ProductVersion.cs')
# Same production window/updater sources with a disposable test verification key.
TEST_WINDOW=WORK/'current.exe'
compile_cs(TEST_WINDOW,ROOT/'standalone/GlassWindow.cs',ROOT/'standalone/WsgTheme.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/SimulatorDiscovery.cs',ROOT/'standalone/ReleaseTrust.cs',signing.public,ROOT/'tools/GlassSettingsPanel.cs',ROOT/'build/standalone/control/ProductVersion.cs',win=True)
fake=WORK/'future.cs'
fake.write_text('using System;using System.IO;using System.Reflection;\n[assembly:AssemblyVersion("'+NEXT+'")]\n[assembly:AssemblyFileVersion("'+NEXT+'")]\nclass Future { static void Main() { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"reopened.txt"),"'+NEXT+'"); } }')
compile_cs(WORK/'future.exe',fake,win=True)
def digest(b):return hashlib.sha256(b).hexdigest()
def encoded(o):return (json.dumps(o,indent=2)+'\n').encode()
def files(version,future=False):
    data={n:(version+' inert '+n).encode() for n in ['Install.ps1','InstallCore.ps1','Update.ps1','Resolve-MSFS.ps1','dxgi.dll','StandaloneGlassGraphics.dll','StandaloneGlass.ini']}
    data[EXE]=(WORK/'future.exe' if future else TEST_WINDOW).read_bytes()
    data['VERSION']=(version+'\n').encode()
    data['licenses/example.txt']=b'fixture license'
    data['new-file.txt' if future else 'retired-file.txt']=b'new' if future else b'old'
    data['payload.json']=encoded({'product':PRODUCT,'version':version,'files':{n:digest(data[n]) for n in ['dxgi.dll','StandaloneGlassGraphics.dll','StandaloneGlass.ini']}})
    return inventory(data,version)
def inventory(data,version=NEXT):
    data=dict(data);data.pop('app-files.json',None)
    data['app-files.json']=encoded(signing.sign({'schema':1,'product':PRODUCT,'version':version,'files':{n:digest(b) for n,b in data.items()}}))
    return data
old=files(CURRENT);fresh=files(NEXT,True)
count=0
def fixture():
    global count
    count+=1;p=WORK/('case-%02d'%count);p.mkdir()
    for n,b in old.items():
        target=p/n;target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(b)
    for n,b in {'Appearance.txt':b'light','SimulatorDirectory.txt':b'fixture path','StandaloneGlass.saved.ini':b'user preferences','notes.txt':b'user data','ofxr_bridge.ini':b'unrelated OFXR settings'}.items():(p/n).write_bytes(b)
    return p
def snap(p):return {f.relative_to(p).as_posix():f.read_bytes() for f in p.rglob('*') if f.is_file() and '.app-update' not in f.parts}
def archive(data):
    out=io.BytesIO()
    with zipfile.ZipFile(out,'w',zipfile.ZIP_DEFLATED) as z:
        for n,b in data.items():z.writestr(n,b)
    return out.getvalue()
def release(zip_bytes,**changes):
    v=signing.sign({'schema':1,'product':PRODUCT,'version':NEXT,'sha256':digest(zip_bytes),'size':len(zip_bytes)});v.update(changes);return v
def invoke(mode,p,*args,code=0):
    r=subprocess.run([str(RUNNER),mode,str(p),*map(str,args)],capture_output=True,text=True,timeout=40)
    assert r.returncode==code,(mode,r.returncode,r.stdout,r.stderr)
    return r.stdout
def prepare(p,data=None,blob=None,meta=None,code=0):
    blob=archive(fresh if data is None else data) if blob is None else blob
    (WORK/'release.zip').write_bytes(blob);(WORK/'release.json').write_bytes(encoded(release(blob) if meta is None else meta))
    return invoke('prepare',p,WORK/'release.json',WORK/'release.zip',code=code)
def assert_updated(p,before):
    expected={n:b for n,b in before.items() if n not in old};expected.update(fresh)
    assert snap(p)==expected,('Unexpected updated files',snap(p).keys(),expected.keys())

p=fixture();invoke('version',p);invoke('gate',p)
before=snap(p);prepare(p);invoke('apply',p);assert_updated(p,before)
assert not (p/'.app-update').exists()
print('PASS normal replacement, retired files, licenses, settings and unrelated files preserved',flush=True)

# Accept the exact official two-file payload, reject missing/extra/mixed layouts.
official=dict(fresh)
for n in ('StandaloneGlassGraphics.dll','StandaloneGlass.ini'):official.pop(n)
official['glass-cockpit-dlss-fix.ini']=b'[overlay]\nenabled=1\n'
official['payload.json']=encoded({'product':PRODUCT,'version':NEXT,'files':{n:digest(official[n]) for n in ('dxgi.dll','glass-cockpit-dlss-fix.ini')}})
official=inventory(official)
p=fixture();prepare(p,data=official);invoke('apply',p)
assert not (p/'StandaloneGlassGraphics.dll').exists() and (p/'glass-cockpit-dlss-fix.ini').read_bytes()==official['glass-cockpit-dlss-fix.ini']
for kind in ('missing','extra','mixed','hash'):
    broken=dict(official);payload=json.loads(broken['payload.json'])
    if kind=='missing':payload['files'].pop('dxgi.dll')
    elif kind=='extra':payload['files']['unexpected.dll']=digest(b'bad')
    elif kind=='mixed':payload['files']['StandaloneGlass.ini']=digest(b'bad')
    else:payload['files']['dxgi.dll']='0'*64
    broken['payload.json']=encoded(payload);broken=inventory(broken)
    p=fixture();before=snap(p);prepare(p,data=broken,code=1);assert snap(p)==before
print('PASS official-DLL payload migration and strict layout/hash rejection',flush=True)

for mode in ['fail','crash','commit-crash','locked']:
    p=fixture();before=snap(p);prepare(p)
    invoke(mode,p,code={'fail':1,'crash':71,'commit-crash':72}.get(mode,0))
    if mode in ['crash','commit-crash']:invoke('recover',p)
    if mode=='commit-crash':assert_updated(p,before)
    else:assert snap(p)==before,mode
    assert not (p/'.app-update').exists(),mode
print('PASS rollback, locked file, hard interruption before and after commit',flush=True)

# Never restore over a file the user changed after a crash.
p=fixture();prepare(p);invoke('crash',p,code=71)
j=json.loads((p/'.app-update/journal.json').read_text());changed=next(c for c in j['changes'] if c['before']!=c['after'] and (p/c['name']).is_file() and digest((p/c['name']).read_bytes())==c['after'])
dest=p/changed['name'];dest.write_bytes(b'user change during interruption');before=snap(p)
invoke('recover',p,code=1);assert snap(p)==before and (p/'.app-update/journal.json').exists()

blob=archive(fresh)
for meta in [release(blob,product='other'),release(blob,schema=2),release(blob,sha256='0'*64),release(blob,size=len(blob)+1),release(blob,version=CURRENT),release(blob,version='0.0.1'),release(blob,version='1.2')]:
    p=fixture();before=snap(p);prepare(p,blob=blob,meta=meta,code=1);assert snap(p)==before
for filename in ['../escape.txt','/escape.txt','C:/escape.txt','..\\escape.txt','CON.txt','trailing.','folder//file','folder/../file','.app-update/owner','README:stream']:
    bad=dict(fresh);bad[filename]=b'bad';p=fixture();before=snap(p)
    prepare(p,data=bad,code=1);assert snap(p)==before and not (WORK/'escape.txt').exists()
for kind in ['duplicate-case','unlisted','bad-hash','bad-exe','bad-payload','wrong-version','symlink','broken-zip']:
    data=dict(fresh)
    if kind=='duplicate-case':data['version']=b'duplicate'
    elif kind=='unlisted':data['extra.txt']=b'not inventoried'
    elif kind=='bad-hash':data['dxgi.dll']=b'changed'
    elif kind=='bad-exe':data[EXE]=old[EXE];data=inventory(data)
    elif kind=='bad-payload':
        payload=json.loads(data['payload.json']);payload['product']='other';data['payload.json']=encoded(payload);data=inventory(data)
    elif kind=='wrong-version':data['VERSION']=b'99.1.0';data=inventory(data)
    bad=archive(data)
    if kind=='symlink':
        output=io.BytesIO(bad)
        with zipfile.ZipFile(output,'a') as z:
            entry=zipfile.ZipInfo('linked');entry.create_system=3;entry.external_attr=0xA1FF<<16;z.writestr(entry,'elsewhere')
        bad=output.getvalue()
    elif kind=='broken-zip':bad=b'not a zip'
    p=fixture();before=snap(p);prepare(p,blob=bad,code=1);assert snap(p)==before
for kind in ['modified','missing','collision','game-folder']:
    p=fixture()
    if kind=='modified':(p/'Install.ps1').write_bytes(b'user modification')
    elif kind=='missing':(p/'Update.ps1').unlink()
    elif kind=='collision':(p/'new-file.txt').write_bytes(b'unrelated file')
    else:(p/'FlightSimulator2024.exe').write_bytes(b'INERT MARKER')
    before=snap(p);prepare(p,code=1);assert snap(p)==before
print('PASS invalid metadata/ZIP/path/hash/payload/version rejection and local-change protection',flush=True)

# Authentication failures must not download or replace anything.
for key,value in [('signature',None),('signature','AA=='),('keyId','0'*64)]:
    p=fixture();before=snap(p);prepare(p,blob=blob,meta=release(blob,**{key:value}),code=1);assert snap(p)==before
for target in ['release.json','source-inventory.json','new/app-files.json','apply.exe']:
    p=fixture();before=snap(p);prepare(p)
    dest=p/'.app-update'/target
    if target=='apply.exe':dest.write_bytes(b'untrusted helper')
    else:
        obj=json.loads(dest.read_text());obj['signature']='AA==';dest.write_bytes(encoded(obj))
    invoke('startup' if target in ['apply.exe','source-inventory.json'] else 'apply',p,code=1)
    assert snap(p)==before
p=fixture();prepare(p);invoke('crash',p,code=71)
journal=p/'.app-update/journal.json';obj=json.loads(journal.read_text())
obj['changes'].append({'name':'notes.txt','before':digest(b'user data'),'after':None});journal.write_bytes(encoded(obj));before=snap(p)
invoke('recover',p,code=1);assert snap(p)==before and (p/'notes.txt').read_bytes()==b'user data'
print('PASS missing/forged signatures, substituted helper and forged recovery journal rejected',flush=True)

for mode in ['offline','same','older','check','defer']:
    p=fixture();before=snap(p)
    (WORK/'release.zip').write_bytes(blob)
    meta=signing.sign(release(blob,version=CURRENT if mode=='same' else '0.0.1' if mode=='older' else NEXT))
    (WORK/'release.json').write_bytes(encoded(meta))
    output=invoke(mode if mode in ['offline','defer'] else 'check',p,WORK/'release.json',WORK/'release.zip')
    if mode=='check':assert 'Restart=True' in output and (p/'launch-requested.txt').exists()
    else:assert 'Restart=False' in output and snap(p)==before
    if mode=='offline':assert 'current version kept' in output
    if mode=='defer':assert (p/'.app-update/release.json').exists()
print('PASS unavailable server, equal/older versions, automatic launch and busy-window deferral',flush=True)

# A junction in an unrecognized staging path must not be followed or removed.
p=fixture();outside=WORK/'outside';outside.mkdir();(outside/'keep').write_bytes(b'preserve')
link=p/'.app-update'
ps=subprocess.run(['powershell.exe','-NoProfile','-NonInteractive','-Command',"New-Item -ItemType Junction -Path '"+str(link)+"' -Target '"+str(outside)+"' | Out-Null"],capture_output=True,text=True)
assert ps.returncode==0,ps.stderr
try:
    invoke('startup',p,code=1);assert (outside/'keep').read_bytes()==b'preserve' and link.is_junction()
finally:os.rmdir(link)

# Exercise the test-key build of the production EXE as the temporary updater, including parent wait
# and reopening the replacement EXE. No simulator or visible GUI is involved.
p=fixture();before=snap(p);prepare(p);invoke('handoff',p)
deadline=time.monotonic()+20
while not (p/'reopened.txt').exists() and time.monotonic()<deadline:time.sleep(.1)
assert (p/'reopened.txt').read_text()==NEXT,'The replacement EXE did not reopen'
(p/'reopened.txt').unlink();assert_updated(p,before)
for _ in range(30):
    invoke('startup',p)
    if not (p/'.app-update').exists():break
    time.sleep(.1)
assert not (p/'.app-update').exists()
print('PASS production-source EXE helper handoff (isolated test key), parent exit wait, new EXE relaunch and cleanup',flush=True)
cleanup_fixture(WORK)
print('PASS all app updater checks ('+str(count)+' isolated fixtures)',flush=True)
