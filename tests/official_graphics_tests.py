"""Check the actual official binary, not the custom renderer's test exports."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys

ROOT=Path(__file__).resolve().parents[1]
lock=json.loads((ROOT/'standalone/official-engine-lock.json').read_text())
WORK=ROOT/'build/official-engine'
fixture=WORK/'fixture'
subprocess.run(['powershell.exe','-NoProfile','-File',str(ROOT/'tests/build_official_fixture.ps1'),'-Source',str(WORK/'upstream-source'),'-Output',str(fixture)],check=True)
dll=WORK/'binary/dxgi.dll'
assert hashlib.sha256(dll.read_bytes()).hexdigest()==lock['dllSha256']
shutil.copy2(dll,fixture/'dxgi.dll')
results=[]
def check(name,args,cwd):
    run=subprocess.run(list(map(str,args)),cwd=cwd,capture_output=True,text=True,timeout=100)
    (WORK/(name+'.txt')).write_text(run.stdout+run.stderr,encoding='utf-8')
    results.append({'name':name,'exit':run.returncode})
    print(name,run.returncode,flush=True)
    assert run.returncode==0,(name,run.stdout,run.stderr)
for mode in ('normal','clone','normal-present1','clone-present1'):
    shutil.copy2(dll,fixture/'startup/dxgi.dll')
    check('startup-'+mode,[fixture/'startup/FlightSimulator2024.exe',fixture/'startup/dxgi.dll',mode],fixture/'startup')
shutil.copy2(dll,fixture/'private/dxgi.dll')
check('private-adapter',[fixture/'private/FlightSimulator2024.exe','wrapped',fixture/'private/dxgi.dll'],fixture/'private')
cases=[[],['--fsr'],['--vr'],['--screen-t0'],['--reload'],['--vr','--screen-t0'],['--track'],['--fsr','--vr','--track'],['--hotpatch'],['--fsr','--optiscaler-fsr'],['--fsr','--optiscaler-fsr','--vr'],['--ngx-proxy'],['--ngx-proxy','--vr'],['--extra-pass'],['--fg-repeat'],['--fsr','--vr','--nested']]
for number,flags in enumerate(cases):check('core-'+str(number),[fixture/'FlightSimulator2024.exe',*flags],fixture)
check('motion-quality',[sys.executable,'-B',ROOT/'tests/upstream_v1_tests.py',fixture/'FlightSimulator2024.exe'],ROOT)
(WORK/'graphics-results.json').write_text(json.dumps(results,indent=2))
print('PASS exact official DLL: 4 startup probes, private adapters/D3D11On12, 16 core scenarios, 13 motion measurements')
