"""Upgrade two real signed packages offline; never run their DLLs or touch MSFS."""
from pathlib import Path
import hashlib
import json
import subprocess
import sys
import tempfile
import zipfile
from fixture_cleanup import cleanup_fixture
from signed_fixture import ROOT, compile_cs

old_zip, feed, new_zip = (Path(s).resolve() for s in sys.argv[1:4])
# Optional historical source proves the already-installed updater accepts the
# transition release, rather than testing only the newly compiled updater.
old_updater = Path(sys.argv[4]).resolve() if len(sys.argv) == 5 else ROOT/'standalone/AppUpdate.cs'
work = Path(tempfile.mkdtemp(prefix='standalone-tests-release-', dir=ROOT/'build')).resolve()
try:
    app = work/'app'
    with zipfile.ZipFile(old_zip) as z:
        z.extractall(app)
    runner = work/'update.exe'
    common = [ROOT/'standalone/AppUpdate.cs', ROOT/'standalone/ReleaseTrust.cs',
              ROOT/'standalone/ReleasePublicKey.cs', ROOT/'build/standalone/control/ProductVersion.cs']
    compile_cs(runner, ROOT/'tests/app_update_runner.cs', old_updater, *common[1:])
    install = work/'install.exe'
    compile_cs(install, ROOT/'tests/native_installer_runner.cs', ROOT/'standalone/NativeInstaller.cs',
               ROOT/'standalone/SimulatorDiscovery.cs', *common)
    game = work/'inert-game'; game.mkdir()
    (game/'FlightSimulator2024.exe').write_bytes(b'INERT MARKER - NEVER EXECUTE')
    (game/'dxgi.dll').write_bytes(b'original third-party proxy fixture')
    ofxr = b'[OFXR]\nLowerVRAM=1\nMultiplier=2\n'
    (game/'ofxr_bridge.ini').write_bytes(ofxr)
    original = {p.name:p.read_bytes() for p in game.iterdir()}
    subprocess.run([str(install), 'install', str(app), str(game), 'replace'], check=True, timeout=30)
    settings = b'[cockpit]\nenabled=1\nline_smoothing=35\n[experimental]\nseam_fix=1\n; existing user settings preserved\n'
    (game/'StandaloneGlass.ini').write_bytes(settings)
    preserved = {'StandaloneGlass.saved.ini': settings, 'Appearance.txt': b'light',
                 'SimulatorDirectory.txt': str(game).encode(), 'ofxr_bridge.ini': ofxr}
    for name, data in preserved.items():
        (app/name).write_bytes(data)
    subprocess.run([str(runner), 'prepare', str(app), str(feed), str(new_zip)], check=True, timeout=30)
    subprocess.run([str(runner), 'apply', str(app)], check=True, timeout=30)
    subprocess.run([str(runner), 'verify', str(app), str(feed)], check=True, timeout=30)
    for name, data in preserved.items():
        assert (app/name).read_bytes() == data, name
    assert not (app/'.app-update').exists()
    inventory = json.loads((app/'app-files.json').read_text())
    for name, digest in inventory['files'].items():
        assert hashlib.sha256((app/name).read_bytes()).hexdigest() == digest, name
    assert {'ENGINE-LICENSE.md', 'licenses/glass/LICENSE', 'licenses/glass/NOTICE.md'} <= inventory['files'].keys()
    subprocess.run([str(install), 'install', str(app), str(game)], check=True, timeout=30)
    assert (game/'StandaloneGlass.ini').read_bytes() == settings
    assert (game/'ofxr_bridge.ini').read_bytes() == ofxr
    for name in ('dxgi.dll', 'StandaloneGlassGraphics.dll'):
        assert (game/name).read_bytes() == (app/name).read_bytes()
    subprocess.run([str(install), 'uninstall', str(app), str(game)], check=True, timeout=30)
    assert {p.name:p.read_bytes() for p in game.iterdir() if p.is_file()} == original
    print('PASS actual signed app upgrade, new licenses, DLL upgrade, settings/OFXR preservation and original proxy restore')
finally:
    cleanup_fixture(work)
