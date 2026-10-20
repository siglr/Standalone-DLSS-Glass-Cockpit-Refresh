"""Prepare a separate, locally signed test package using the untouched official DLL.

No production signing key, update feed, MSFS installation or published upload folder is used.
The pinned corresponding upstream source, including submodules, ships alongside the DLL.
"""
from pathlib import Path
import hashlib
import json
import os
import shutil
import subprocess
import sys
import urllib.request
import zipfile

ROOT = Path(__file__).resolve().parents[1]
WORK = ROOT / 'build/official-engine'
VERSION = (ROOT / 'standalone/VERSION').read_text().strip()
LOCK = json.loads((ROOT/'standalone/official-engine-lock.json').read_text(encoding='utf-8'))
COMMIT, REPOSITORY = LOCK['commit'], LOCK['repository']
ZIP_HASH, DLL_HASH = LOCK['zipSha256'], LOCK['dllSha256']
PRODUCT = 'Standalone-DLSS-Glass-Cockpit-Refresh'

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def fetch(url, path, limit=64*1024*1024, refresh=False):
    if path.exists() and not refresh:
        return
    with urllib.request.urlopen(urllib.request.Request(url, headers={'User-Agent': PRODUCT}), timeout=30) as response:
        data = response.read(limit+1)
    if len(data)>limit:
        raise RuntimeError('Download exceeded size limit')
    path.write_bytes(data)

def prepare():
    WORK.mkdir(parents=True, exist_ok=True)
    app=(ROOT/'standalone/UpstreamRelease.cs').read_text(encoding='utf-8')
    assert ZIP_HASH in app and DLL_HASH in app and ('Version="'+LOCK['release'][1:]+'"') in app, 'App and engine lock disagree'
    fetch(f'https://github.com/{REPOSITORY}/releases/download/v1.0.1/glass-cockpit-dlss-fix-standalone.zip', WORK/'official.zip')
    assert digest(WORK/'official.zip')==ZIP_HASH, 'Official archive checksum differs; do not use it'
    binary=WORK/'binary';binary.mkdir(exist_ok=True)
    with zipfile.ZipFile(WORK/'official.zip') as archive:
        for name in ('dxgi.dll','LICENSE','NOTICE.md','README.txt'):
            (binary/name).write_bytes(archive.read(name))
    assert digest(binary/'dxgi.dll')==DLL_HASH
    addon=binary/'glass-cockpit-dlss-fix.addon64'
    fetch(f'https://github.com/{REPOSITORY}/releases/download/v1.0.1/{addon.name}',addon)
    assert digest(addon)==LOCK['addon']['sha256'] and LOCK['addon']['sha256'] in app
    # The add-on is built from the release tag, not the standalone's main commit.
    fetch(f"https://api.github.com/repos/{REPOSITORY}/zipball/{LOCK['addon']['tagObject']}",WORK/'addon-source.zip')
    fetch(f"https://api.github.com/repos/{REPOSITORY}/releases/{LOCK['releaseId']}",WORK/'official-release.json',limit=1024*1024,refresh=True)
    fetch(f'https://api.github.com/repos/{REPOSITORY}/zipball/{COMMIT}',WORK/'source.zip')
    source=WORK/'upstream-source';source.mkdir(exist_ok=True)
    dependencies=[('',WORK/'source.zip')]
    for name, repo, commit in (
        ('minhook','TsudaKageyu/minhook','8af6b4acae5a9388fd742b56fa79ece89d96f823'),
        ('reshade','crosire/reshade','6a7eaf8dbf5ed7050de1afd381ff058dfb51935e')):
        path=WORK/(name+'-source.zip')
        fetch(f'https://api.github.com/repos/{repo}/zipball/{commit}',path)
        dependencies.append(('third_party/'+name+'/',path))
    source_zip=WORK/'IFreemz-standalone-v1.0.1-source.zip'
    with zipfile.ZipFile(source_zip,'w',zipfile.ZIP_DEFLATED) as out:
        seen=set()
        for prefix,path in dependencies:
            with zipfile.ZipFile(path) as archive:
                for item in archive.infolist():
                    if item.is_dir():continue
                    name=prefix+item.filename.split('/',1)[1]
                    if '..' in Path(name).parts or Path(name).is_absolute():raise RuntimeError('Unsafe upstream source name')
                    if name in seen:raise RuntimeError('Duplicate source name')
                    seen.add(name);data=archive.read(item)
                    dest=source/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data)
                    out.writestr(name,data)
        out.writestr('BUNDLED-SOURCE.json',json.dumps({'repository':REPOSITORY,'commit':COMMIT,'release':'v1.0.1','dllSha256':DLL_HASH},indent=2))
    with zipfile.ZipFile(WORK/'IFreemz-addon-v1.0.1-source.zip','w',zipfile.ZIP_DEFLATED) as out:
        for prefix,path in [('',WORK/'addon-source.zip'),*dependencies[1:]]:
            with zipfile.ZipFile(path) as archive:
                for item in archive.infolist():
                    if item.is_dir():continue
                    name=prefix+item.filename.split('/',1)[1]
                    if '..' in Path(name).parts or Path(name).is_absolute():raise RuntimeError('Unsafe add-on source name')
                    out.writestr(name,archive.read(item))
        out.writestr('BUNDLED-SOURCE.json',json.dumps({'repository':REPOSITORY,'tagObject':LOCK['addon']['tagObject'],'commit':LOCK['addon']['commit'],'release':'v1.0.1','addonSha256':digest(addon),'sourceSubmodules':LOCK['sourceSubmodules']},indent=2))
    return binary,source_zip

def main():
    binary,source_zip=prepare()
    # Isolated development key. Never discover the maintainer's production key.
    sys.path.insert(0,str(ROOT/'tests'))
    from signed_fixture import Signing,compile_cs
    signing_dir=WORK/'signing';signing_dir.mkdir(exist_ok=True)
    signing=Signing(signing_dir)
    out=WORK/'package';out.mkdir(exist_ok=True)
    version_source=WORK/'ProductVersion.cs'
    version_source.write_text((ROOT/'standalone/ProductVersion.cs.in').read_text().replace('@GLASS_FILE_VERSION@',VERSION).replace('@PROJECT_VERSION@',VERSION))
    sources=[ROOT/'standalone'/name for name in ('GlassWindow.cs','AppUpdate.cs','NativeInstaller.cs','SimulatorDiscovery.cs','ReleaseTrust.cs','WsgTheme.cs','UpstreamRelease.cs')]
    compile_cs(out/(PRODUCT+'.exe'),*sources,ROOT/'tools/GlassSettingsPanel.cs',signing.public,version_source,win=True,defines=('GLASS_NO_AUTO_UPDATE',))
    verifier=WORK/'verify-release.exe'
    compile_cs(verifier,ROOT/'tests/official_engine_runner.cs',ROOT/'standalone/NativeInstaller.cs',ROOT/'standalone/AppUpdate.cs',ROOT/'standalone/ReleaseTrust.cs',signing.public,version_source)
    verified=subprocess.check_output([str(verifier),'archive',str(WORK/'official-release.json'),str(WORK/'official.zip')],text=True).strip()
    assert verified==DLL_HASH, 'Release-note checksums do not match the bundled official DLL'
    addon_verified=subprocess.check_output([str(verifier),'archive',str(WORK/'official-release.json'),str(binary/'glass-cockpit-dlss-fix.addon64'),'addon'],text=True).strip()
    assert addon_verified==digest(binary/'glass-cockpit-dlss-fix.addon64')
    items={
        'dxgi.dll':binary/'dxgi.dll',
        'glass-cockpit-dlss-fix.addon64':binary/'glass-cockpit-dlss-fix.addon64',
        'Source code/IFreemz-addon-v1.0.1-source.zip':WORK/'IFreemz-addon-v1.0.1-source.zip',
        'glass-cockpit-dlss-fix.ini':ROOT/'standalone/glass-cockpit-dlss-fix.ini',
        'licenses/IFreemz/LICENSE':binary/'LICENSE',
        'licenses/IFreemz/NOTICE.md':binary/'NOTICE.md',
        'licenses/IFreemz/README.txt':binary/'README.txt',
        'Source code/'+source_zip.name:source_zip,
        'Source code/README.txt':ROOT/'standalone/SOURCE-CODE-README.txt',
        'LICENSE.md':ROOT/'LICENSE.md',
        'README.txt':ROOT/'standalone/USER-README.txt',
        'official-engine-lock.json':ROOT/'standalone/official-engine-lock.json',
        'official-release.json':WORK/'official-release.json',
        **{name:ROOT/'standalone/LegacyInstallerNotice.ps1' for name in ('Install.ps1','InstallCore.ps1','Update.ps1','Resolve-MSFS.ps1')},
    }
    # Remove only unchanged generated files retired from the previous package layout.
    old_inventory=json.loads((out/'app-files.json').read_text()) if (out/'app-files.json').exists() else {}
    for name in ('IFreemz-addon-v1.0.1-source.zip',source_zip.name,'README.md','COMPARISON.md','IFREEMZ-ENHANCEMENTS.md'):
        obsolete=out/name
        if obsolete.exists() and digest(obsolete)==old_inventory.get('files',{}).get(name):
            assert obsolete.resolve().parent==out.resolve() and not obsolete.is_symlink()
            obsolete.unlink()
    for name,path in items.items():
        dest=out/name;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,dest)
    (out/'VERSION').write_text(VERSION+'\n')
    payload=['dxgi.dll','glass-cockpit-dlss-fix.ini']
    (out/'payload.json').write_text(json.dumps({'product':PRODUCT,'version':VERSION,'files':{name:digest(out/name) for name in payload}},indent=2))
    names=[PRODUCT+'.exe',*items,'VERSION','payload.json']
    inventory={'schema':1,'product':PRODUCT,'version':VERSION,'files':{name:digest(out/name) for name in names}}
    (out/'app-files.json').write_text(json.dumps(signing.sign(inventory),indent=2))
    package=WORK/(PRODUCT+'-test.zip')
    with zipfile.ZipFile(package,'w',zipfile.ZIP_DEFLATED) as archive:
        for name in [*names,'app-files.json']:archive.write(out/name,name)
    package.with_suffix('.zip.sha256').write_text(digest(package)+'  '+package.name+'\n')
    print(package)
    print('Official DLL verified; local test signing; app auto-update disabled; nothing installed or published.')

if __name__=='__main__':main()
