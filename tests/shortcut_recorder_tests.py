"""Exercise focus-local shortcut recording without desktop input or simulator writes."""
from pathlib import Path
import subprocess
import tempfile
from fixture_cleanup import cleanup_fixture
from signed_fixture import ROOT, compile_cs

work = Path(tempfile.mkdtemp(prefix='standalone-tests-shortcuts-', dir=ROOT/'build'))
try:
    runner = work/'shortcuts.exe'
    compile_cs(runner, ROOT/'tests/shortcut_recorder_tests.cs',
               ROOT/'tools/GlassSettingsPanel.cs', ROOT/'standalone/WsgTheme.cs')
    subprocess.run([str(runner)], check=True, timeout=30)
finally:
    cleanup_fixture(work)
