"""Build an installable local package with your own key and official updates disabled."""
from pathlib import Path
import os,subprocess,sys,json
ROOT=Path(__file__).resolve().parents[1]
work=ROOT/'build/development-signing';work.mkdir(parents=True,exist_ok=True)
key=work/'keys/development-key.dpapi';signer=work/'signer.exe';public=work/'ReleasePublicKey.cs'
csc=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
env=os.environ.copy();env['LIB']=''
def call(*args):subprocess.run(list(map(str,args)),cwd=ROOT,env=env,check=True)
call(csc,'/nologo','/out:'+str(signer),'/reference:System.Security.dll','/reference:System.Web.Extensions.dll',ROOT/'tools/ReleaseSigner.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs')
xml=subprocess.check_output([str(signer),'public' if key.exists() else 'init',str(key)],text=True).strip()
public.write_text('internal static class ReleasePublicKey { internal const string Xml = '+json.dumps(xml)+'; }',encoding='utf-8')
call(sys.executable,'-B','scripts/build.py','-S','.', '-B','build/standalone','-G','Visual Studio 17 2022','-A','x64','-DGLASS_ENABLE_OFFICIAL_UPDATES=OFF','-DGLASS_PUBLIC_KEY_FILE='+str(public))
call(sys.executable,'-B','scripts/build.py','--build','build/standalone','--config','Release','--parallel','8')
env['GLASS_SIGNING_KEY']=str(key)
call(sys.executable,'-B','standalone/package.py','--development')
print('Your development key stays under ignored build/development-signing/keys. No files were installed into MSFS.')
