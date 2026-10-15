"""Independent disposable signing keys. Never signs fixtures with the publisher key."""
from pathlib import Path
import os,subprocess,json
ROOT=Path(__file__).resolve().parents[1]
CSC=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
def compile_cs(out,*sources,win=False,defines=()):
 env=os.environ.copy();env['LIB']=''
 subprocess.run([str(CSC),'/nologo','/platform:x64','/target:'+('winexe' if win else 'exe'),'/out:'+str(out),*['/define:'+value for value in defines],'/reference:System.Security.dll','/reference:System.Web.Extensions.dll','/reference:System.IO.Compression.dll','/reference:System.IO.Compression.FileSystem.dll','/reference:System.Windows.Forms.dll','/reference:System.Drawing.dll',*map(str,sources)],check=True,env=env)
class Signing:
 def __init__(self,work):
  self.work=work;self.signer=work/'signer.exe';self.key=work/'keys/test-key.dpapi';self.public=work/'TestPublicKey.cs'
  compile_cs(self.signer,ROOT/'tools/ReleaseSigner.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs')
  xml=subprocess.check_output([str(self.signer),'init',str(self.key)],text=True).strip()
  self.public.write_text('internal static class ReleasePublicKey { internal const string Xml = '+json.dumps(xml)+'; }')
 def sign(self,obj):
  path=self.work/'unsigned-fixture.json';path.write_text(json.dumps(obj))
  subprocess.run([str(self.signer),'sign',str(self.key),str(path)],check=True)
  return json.loads(path.read_text(encoding='utf-8-sig'))
