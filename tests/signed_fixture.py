"""Independent disposable signing keys. Never signs fixtures with the publisher key."""
from pathlib import Path
import os,subprocess,json
ROOT=Path(__file__).resolve().parents[1]
CSC=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
def compile_cs(out,*sources,win=False,defines=()):
 if any(Path(s).name in ('GlassWindow.cs','NativeInstaller.cs') for s in sources) and not any(Path(s).name=='UpstreamRelease.cs' for s in sources):sources=(*sources,ROOT/'standalone/UpstreamRelease.cs')
 env=os.environ.copy();env['LIB']=''
 subprocess.run([str(CSC),'/nologo','/codepage:65001','/platform:x64','/target:'+('winexe' if win else 'exe'),'/out:'+str(out),*(['/win32icon:'+str(ROOT/'standalone/assets/WeSimGlide.ico'),'/resource:'+str(ROOT/'standalone/assets/WeSimGlide.ico')+',WeSimGlide.ico','/resource:'+str(ROOT/'standalone/assets/WeSimGlide.png')+',WeSimGlide.png'] if any(Path(source).name=='GlassWindow.cs' for source in sources) else []),*['/define:'+value for value in defines],'/reference:System.Security.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll',*map(str,sources)],check=True,env=env)
class Signing:
 def __init__(self,work):
  self.work=work;self.signer=work/'signer.exe';self.key=work/'keys/test-key.dpapi';self.public=work/'TestPublicKey.cs'
  compile_cs(self.signer,ROOT/'tools/ReleaseSigner.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs')
  xml=subprocess.check_output([str(self.signer),'public' if self.key.exists() else 'init',str(self.key)],text=True).strip()
  self.public.write_text('internal static class ReleasePublicKey { internal const string Xml = '+json.dumps(xml)+'; }')
 def sign(self,obj):
  path=self.work/'unsigned-fixture.json';path.write_text(json.dumps(obj))
  subprocess.run([str(self.signer),'sign',str(self.key),str(path)],check=True)
  return json.loads(path.read_text(encoding='utf-8-sig'))
