"""Validate the locally signed ZIP in an isolated dummy game directory."""
from pathlib import Path
import subprocess, tempfile, zipfile
from fixture_cleanup import cleanup_fixture
from signed_fixture import ROOT, compile_cs

work = Path(tempfile.mkdtemp(prefix='standalone-tests-development-', dir=ROOT/'build')).resolve()
public = ROOT/'build/development-signing/ReleasePublicKey.cs'
version = ROOT/'build/standalone/control/ProductVersion.cs'
common = [ROOT/'standalone/AppUpdate.cs', ROOT/'standalone/ReleaseTrust.cs', public, version]
runner = work/'native.exe'
compile_cs(runner, ROOT/'tests/native_installer_runner.cs', ROOT/'standalone/NativeInstaller.cs',
           ROOT/'standalone/SimulatorDiscovery.cs', *common)
policy = work/'policy.exe'
compile_cs(policy, ROOT/'tests/source_policy_runner.cs', *common, defines=('GLASS_NO_AUTO_UPDATE',))
subprocess.run([str(policy)], check=True, timeout=15)
package = work/'package'
with zipfile.ZipFile(ROOT/'build/Standalone-DLSS-Glass-Cockpit-Refresh-development.zip') as archive:
    assert not any(name.endswith(('.dpapi', '.pfx', '.pem', '.key')) for name in archive.namelist())
    assert {'LICENSE.md', 'NOTICE.md'} <= set(archive.namelist())
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
