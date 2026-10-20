"""Validate the locally signed ZIP in an isolated dummy game directory."""
from pathlib import Path
import subprocess, tempfile, zipfile, json, hashlib, io
from fixture_cleanup import cleanup_fixture
from signed_fixture import ROOT, compile_cs

work = Path(tempfile.mkdtemp(prefix='standalone-tests-development-', dir=ROOT/'build')).resolve()
official = (ROOT/'standalone/UpstreamRelease.cs').exists()
public = ROOT/('build/official-engine/signing/TestPublicKey.cs' if official else 'build/development-signing/ReleasePublicKey.cs')
version = ROOT/('build/official-engine/ProductVersion.cs' if official else 'build/standalone/control/ProductVersion.cs')
common = [ROOT/'standalone/AppUpdate.cs', ROOT/'standalone/ReleaseTrust.cs', public, version]
runner = work/'native.exe'
compile_cs(runner, ROOT/'tests/native_installer_runner.cs', ROOT/'standalone/NativeInstaller.cs',
           ROOT/'standalone/SimulatorDiscovery.cs', *common)
policy = work/'policy.exe'
compile_cs(policy, ROOT/'tests/source_policy_runner.cs', *common, defines=('GLASS_NO_AUTO_UPDATE',))
subprocess.run([str(policy)], check=True, timeout=15)
package = work/'package'
zip_path = ROOT/('build/official-engine/Standalone-DLSS-Glass-Cockpit-Refresh-test.zip' if official else 'build/Standalone-DLSS-Glass-Cockpit-Refresh-development.zip')
with zipfile.ZipFile(zip_path) as archive:
    assert not any(name.endswith(('.dpapi', '.pfx', '.pem', '.key')) for name in archive.namelist())
    assert {'LICENSE.md', 'licenses/IFreemz/NOTICE.md' if official else 'NOTICE.md'} <= set(archive.namelist())
    if official:
        lock=json.loads((ROOT/'standalone/official-engine-lock.json').read_text())
        assert json.loads(archive.read('official-engine-lock.json'))==lock
        assert hashlib.sha256(archive.read('dxgi.dll')).hexdigest()==lock['dllSha256']
        assert hashlib.sha256(archive.read('glass-cockpit-dlss-fix.addon64')).hexdigest()==lock['addon']['sha256']
        expected_sources={'Source code/IFreemz-standalone-v1.0.2-source.zip','Source code/IFreemz-addon-v1.0.2-source.zip'}
        assert {n for n in archive.namelist() if n.endswith('-source.zip')}==expected_sources
        for mode,commit,cache in [('standalone',lock['commit'],'source.zip'),('addon',lock['addon']['commit'],'addon-source.zip')]:
            with zipfile.ZipFile(io.BytesIO(archive.read(f'Source code/IFreemz-{mode}-{lock["release"]}-source.zip'))) as source:
                manifest=json.loads(source.read('BUNDLED-SOURCE.json'))
                assert manifest['commit']==commit and manifest['release']==lock['release']
                assert manifest['sourceSubmodules']==lock['sourceSubmodules']
                with zipfile.ZipFile(ROOT/'build/official-engine'/cache) as upstream:
                    prefix=upstream.namelist()[0].split('/')[0]+'/'
                    archive_ref=lock['addon']['tagObject'] if mode=='addon' else commit
                    assert archive_ref[:7] in prefix, 'Cached source is from the wrong pinned commit/tag object'
                    for name in ('src/glass_cockpit_dlss_fix.cpp','LICENSE','NOTICE.md'):
                        assert source.read(name)==upstream.read(prefix+name), (mode,name)
                assert any(n.startswith('third_party/minhook/') for n in source.namelist())
                assert any(n.startswith('third_party/reshade/') for n in source.namelist())
        assert 'Source code/README.txt' in archive.namelist()
        assert 'README.txt' in archive.namelist()
        assert not {'README.md','COMPARISON.md','IFREEMZ-ENHANCEMENTS.md'} & set(archive.namelist())
        assert {n for n in archive.namelist() if n.endswith('.md')}=={'LICENSE.md','licenses/IFreemz/NOTICE.md'}
    archive.extractall(package)
game = work/'dummy-game'; game.mkdir()
(game/'FlightSimulator2024.exe').write_bytes(b'INERT TEST MARKER - NEVER EXECUTE')
(game/'ofxr_bridge.ini').write_bytes(b'preserve independent settings')
before = {p.name:p.read_bytes() for p in game.iterdir()}
for operation in ['install', 'uninstall']:
    subprocess.run([str(runner), operation, str(package), str(game)], check=True, timeout=20)
assert {p.name:p.read_bytes() for p in game.iterdir()} == before
cleanup_fixture(work)
print('PASS real development ZIP signature, install/uninstall and independent settings preservation')
