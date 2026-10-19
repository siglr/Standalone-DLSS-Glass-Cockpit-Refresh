"""Untouched official add-on under real ReShade, using isolated upstream GPU fixtures.

Run official_graphics_tests.py first to build the host. The ReShade test dependency
is extracted locally from the official 6.8.0 full-add-on download; never distributed.
"""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys

ROOT=Path(__file__).resolve().parents[1]
base=ROOT/'build/official-engine'
source=base/'fixture';target=base/'addon-fixture';target.mkdir(exist_ok=True)
addon=base/'binary/glass-cockpit-dlss-fix.addon64'
reshade=base/'test-dependencies/ReShade64.asi'
assert hashlib.sha256(addon.read_bytes()).hexdigest()=='a637456d2a93780ccfea73043f428e4f2a0f5f009423a3ca22b694b198565517'
assert hashlib.sha256(reshade.read_bytes()).hexdigest()=='0cee63f9c9f13f3ac909c5b4903f4dbb4b719a7ab3b4f13b0deaf83c814b94f7'
assert not (target/'dxgi.dll').exists(),'Do not load both fixes'
for path in source.iterdir():
    if path.is_file() and (path.suffix in ('.cso','.exe','.asi') or path.name in ('_nvngx.dll','amd_fidelityfx_dx12.dll')):shutil.copy2(path,target/path.name)
(target/'OptiScaler').mkdir(exist_ok=True)
shutil.copy2(source/'OptiScaler/amd_fidelityfx_upscaler_dx12.dll',target/'OptiScaler/amd_fidelityfx_upscaler_dx12.dll')
shutil.copy2(addon,target/addon.name);shutil.copy2(reshade,target/reshade.name)
results=[]
cases=[[],['--fsr'],['--vr'],['--screen-t0'],['--reload'],['--vr','--screen-t0'],['--track'],['--fsr','--vr','--track'],['--hotpatch'],['--fsr','--optiscaler-fsr'],['--fsr','--optiscaler-fsr','--vr'],['--ngx-proxy'],['--ngx-proxy','--vr'],['--extra-pass'],['--fg-repeat'],['--fsr','--vr','--nested']]
for index,flags in enumerate(cases):
    run=subprocess.run([str(target/'FlightSimulator2024.exe'),*flags],cwd=target,capture_output=True,text=True,timeout=100)
    (base/f'addon-core-{index}.txt').write_text(run.stdout+run.stderr,encoding='utf-8')
    results.append({'flags':flags,'exit':run.returncode})
    print('addon-core-'+str(index),run.returncode,flush=True)
    assert run.returncode==0,run.stdout+run.stderr
run=subprocess.run([sys.executable,'-B',str(ROOT/'tests/upstream_v1_tests.py'),str(target/'FlightSimulator2024.exe')],cwd=ROOT,capture_output=True,text=True,timeout=400)
(base/'addon-motion.txt').write_text(run.stdout+run.stderr,encoding='utf-8');assert run.returncode==0,run.stdout+run.stderr
(base/'addon-graphics-results.json').write_text(json.dumps(results,indent=2))
print('PASS official add-on with ReShade 6.8.0: 16 core scenarios and 13 motion measurements')
