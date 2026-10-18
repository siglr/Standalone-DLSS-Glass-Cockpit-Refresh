"""Check the exact packaged DLLs through native/loader startup and copied tables.

Runs isolated fake-simulator probes only; never installs or launches MSFS.
"""
from pathlib import Path
import hashlib
import json
import shutil
import subprocess
import sys
import tempfile
import zipfile
from fixture_cleanup import cleanup_fixture

ROOT = Path(__file__).resolve().parents[1]
archive = Path(sys.argv[1]).resolve()
work = Path(tempfile.mkdtemp(prefix='standalone-tests-startup-', dir=ROOT/'build')).resolve()
try:
    with zipfile.ZipFile(archive) as z:
        inventory = json.loads(z.read('app-files.json'))
        payload = {name: z.read(name) for name in ('dxgi.dll', 'StandaloneGlassGraphics.dll')}
        for name, data in payload.items():
            assert hashlib.sha256(data).hexdigest() == inventory['files'][name], name
    for mode in ('wrapped-adapter', 'bootstrap'):
        folder = work/mode; folder.mkdir()
        probe = folder/'FlightSimulator2024.exe'
        shutil.copy2(ROOT/'build/standalone/probe-native/Release/FlightSimulator2024.exe', probe)
        for name, data in payload.items():
            if mode == 'bootstrap' or name != 'dxgi.dll':
                (folder/name).write_bytes(data)
        for clone in (False, True):
            args = [str(probe), mode, str(folder/'StandaloneGlassGraphics.dll')]
            if clone: args.append('--cloned-swapchain')
            run = subprocess.run(args, cwd=folder, capture_output=True, text=True, timeout=30)
            assert run.returncode == 0, (mode, clone, run.returncode, run.stdout, run.stderr)
            print(f'PASS packaged {inventory["version"]}: {mode}, copied table={clone}', flush=True)
finally:
    cleanup_fixture(work)
