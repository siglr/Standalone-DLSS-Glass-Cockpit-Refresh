"""Maintainer-only release signing. Never copies private key material into a package."""
from pathlib import Path
import os, subprocess

ROOT=Path(__file__).resolve().parents[1]
SIGNER=ROOT/'build/signing/ReleaseSigner.exe'
# Explicit opt-in only: never discover or use a maintainer's production key implicitly.
KEY=Path(os.environ['GLASS_SIGNING_KEY']).resolve() if os.environ.get('GLASS_SIGNING_KEY') else None

def prepare():
    sources=[ROOT/'tools/ReleaseSigner.cs',ROOT/'standalone/ReleaseTrust.cs',ROOT/'standalone/ReleasePublicKey.cs']
    SIGNER.parent.mkdir(parents=True,exist_ok=True)
    if not SIGNER.exists() or any(p.stat().st_mtime>SIGNER.stat().st_mtime for p in sources):
        csc=Path(os.environ['WINDIR'])/'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
        env=os.environ.copy();env['LIB']=''
        subprocess.run([str(csc),'/nologo','/out:'+str(SIGNER),'/reference:System.Security.dll','/reference:System.Web.Extensions.dll',*map(str,sources)],check=True,env=env)
    if KEY is None or not KEY.is_file():raise RuntimeError('Set GLASS_SIGNING_KEY to your protected signing key, or use scripts/dev_package.py.')
    return subprocess.check_output([str(SIGNER),'public',str(KEY)],text=True).strip()

def sign(path):
    subprocess.run([str(SIGNER),'sign',str(KEY),str(path)],check=True)
