"""Read-only graphics-helper identification; inert version resources, never loaded."""
from pathlib import Path
import hashlib,json,subprocess,tempfile
from signed_fixture import ROOT,compile_cs
from fixture_cleanup import cleanup_fixture

work=Path(tempfile.mkdtemp(prefix='standalone-tests-compatibility-',dir=ROOT/'build'))
base=ROOT/'build/official-engine'
count=0
try:
    runner=work/'runner.exe'
    compile_cs(runner,ROOT/'tests/official_engine_runner.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',base/'signing/TestPublicKey.cs',base/'ProductVersion.cs')
    def fixture(name,files):
        folder=work/name;folder.mkdir()
        for filename,product in files.items():
            source=work/'resource.cs'
            source.write_text('using System.Reflection; [assembly:AssemblyProduct('+json.dumps(product)+')] [assembly:AssemblyFileVersion("4.9.0.0")] class Inert { static void Main() {} }',encoding='utf-8')
            compile_cs(folder/filename,source)
        return folder
    def check(folder,expected,addon=False,absent=()):
        global count
        before={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.iterdir()}
        result=subprocess.run([str(runner),'compatibility',str(folder),*(['addon'] if addon else [])],capture_output=True,text=True,check=True).stdout
        assert all(item in result for item in expected),(expected,result)
        assert all(item not in result for item in absent),(absent,result)
        assert before=={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in folder.iterdir()}
        count+=1
    folder=fixture('dlss',{'version.dll':'DLSS Enabler'})
    check(folder,['version.dll','DLSS Enabler','4.9.0.0','Reported conflict'])
    check(folder,['Possible interaction'],addon=True,absent=['Reported conflict'])
    folder=fixture('unknown',{'version.dll':'','winmm.dll':'Other graphics tool'})
    check(folder,['Unidentified product','Other graphics tool','Possible interaction'],absent=['DLSS Enabler','Reported conflict'])
    folder=fixture('mfg',{'winmm.dll':'MFG Unlocker'})
    check(folder,['IFreemz reports','not a compatibility guarantee'])
    folder=fixture('mfg-optiscaler',{'winmm.dll':'MFG Unlocker','dxgi.dll':'OptiScaler'})
    check(folder,['Reported startup issue','OptiScaler-plugin'])
    folder=fixture('mfg-optiscaler-separate',{'winmm.dll':'MFG Unlocker','OptiScaler.dll':'OptiScaler'})
    check(folder,['Reported startup issue','OptiScaler-plugin'])
    folder=fixture('reshade',{'dxgi.dll':'ReShade'})
    check(folder,['Choose ReShade add-on'])
    check(folder,['Possible interaction'],addon=True,absent=['Choose ReShade add-on'])
    folder=fixture('optiscaler',{'dxgi.dll':'OptiScaler'})
    check(folder,['also needs','does not guarantee compatibility'])
    folder=fixture('empty',{})
    check(folder,['No graphics helper DLLs found'],absent=['Possible interaction'])
    folder=fixture('sanitized',{'version.dll':'Other\n\u202eProduct'})
    check(folder,['OtherProduct'],absent=['\u202e','Other\n'])
    print(f'PASS {count} compatibility identification/advice scenarios; all files unchanged')
finally:
    cleanup_fixture(work)
