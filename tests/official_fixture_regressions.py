"""Extend upstream's fake-simulator fixture; the tested official DLL is never rebuilt.

Adds external-INI enable/resume and first-recognition bright-glare scenarios.
Only the isolated test host source is changed. Measurements are retained under build.
"""
from pathlib import Path
import shutil
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[1]
WORK=ROOT/'build/official-engine'
source=WORK/'upstream-source'
generated=WORK/'regression-source'
for folder in ('tests','src'):
    (generated/folder).mkdir(parents=True,exist_ok=True)
for path in (source/'tests').glob('*'):
    if path.is_file():shutil.copy2(path,generated/'tests'/path.name)
for name in ('ngx_interface.hpp','ffx_interface.hpp','cockpit_vertex.hlsli'):
    shutil.copy2(source/'src'/name,generated/'src'/name)
path=generated/'tests/fixture.cpp'
text=path.read_text()
def replace(old,new):
    global text
    assert text.count(old)==1,old
    text=text.replace(old,new)
replace('        for (const auto& f : frames) {','''        const bool live_ini = wcsstr(GetCommandLineW(), L"--external-settings") != nullptr;
        const bool first_glare = wcsstr(GetCommandLineW(), L"--first-glare") != nullptr;
        for (const auto& f : frames) {
            if (live_ini) {
                require(WritePrivateProfileStringW(L"overlay",L"enabled",std::strcmp(f.name,"scroll_3")?L"1":L"0",
                    (here / L"glass-cockpit-dlss-fix.ini").c_str()),"INI update failed");
                Sleep(1200);
            }''')
replace('            auto p = pattern(x, (y + scroll + (second ? 384 : 0)) % 768);','''            auto p = pattern(x, (y + scroll + (second ? 384 : 0)) % 768);
                if (wcsstr(GetCommandLineW(),L"--first-glare")) {
                    const bool line=(x%96<2 && ((y+scroll)%96<42)) || ((y+scroll)%96<2 && x%96<42);
                    p=line?std::array<uint8_t,4>{180,180,180,255}:std::array<uint8_t,4>{2,2,2,255};
                }''')
replace('            material[5] = {0, 0, f.dirt, f.shadow_u};','''            material[5] = {0, 0, f.dirt, f.shadow_u};
            if(wcsstr(GetCommandLineW(),L"--first-glare")) { material[4]={0.4f,0.5f,0.6f,4.0f};material[5]={}; }''')
replace('            const int variant = f.camera == 1 ? 1 : (f.glare ? 2 : 0);','            const int variant = first_glare ? 0 : f.camera == 1 ? 1 : (f.glare ? 2 : 0);')
replace('            bool pass = true;','''            bool pass = true;''')
replace('            all_passed &= pass;','''            if(live_ini && !std::strcmp(f.name,"scroll_3")) pass=replaced==0;
            if(live_ini && !std::strcmp(f.name,"scroll_4")) pass=std::isfinite(mean);
            if(live_ini && !std::strcmp(f.name,"scroll_5")) pass &= replaced>10000 && mean<ghost_mean;
            if(first_glare && f.prefill==Frame::perfect && f.jitter_positive) {
                pass &= mean<0.02;
                std::printf("first-glare %s error=%.6f %s\\n",f.name,mean,pass?"PASS":"FAIL");
            }
            if(live_ini)std::printf("external-settings %s %s\\n",f.name,pass?"PASS":"FAIL");
            all_passed &= pass;''')
path.write_text(text)
output=WORK/'regression-fixture'
subprocess.run(['powershell.exe','-NoProfile','-File',str(ROOT/'tests/build_official_fixture.ps1'),'-Source',str(generated),'-Output',str(output)],check=True)
shutil.copy2(WORK/'binary/dxgi.dll',output/'dxgi.dll')
import os,json
env=os.environ.copy();env['GLASS_FIXTURE_SETTINGS']='1'
results=[]
for flags,dark in ((['--external-settings'],False),(['--vr','--external-settings'],False),
                   (['--first-glare'],False),(['--vr','--first-glare'],False),
                   (['--first-glare'],True),(['--vr','--first-glare'],True)):
    settings='[overlay]\nenabled=1\nline_smoothing=35\n[experimental]\ndark_screen_fix='+('1' if dark else '0')+'\n'
    (output/'glass-cockpit-dlss-fix.ini').write_text(settings)
    run=subprocess.run([str(output/'FlightSimulator2024.exe'),*flags],cwd=output,env=env,capture_output=True,text=True,timeout=100)
    log='regression-'+str(len(results))+'.txt';(WORK/log).write_text(run.stdout+run.stderr)
    results.append({'flags':flags,'dark_screen_fix':dark,'exit':run.returncode,'log':log})
    print(flags,'dark_screen_fix='+str(dark),run.returncode,flush=True)
(WORK/'regression-results.json').write_text(json.dumps(results,indent=2))
sys.exit(0 if all(r['exit']==0 for r in results) else 1)
