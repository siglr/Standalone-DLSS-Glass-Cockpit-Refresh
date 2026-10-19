"""Offline paint regression with a known-broken control as a negative control.

No simulator files, desktop input, screenshots or installed settings are used.
The negative control removes only the background fill that fixed v1.0.2.12.
It must compile successfully and fail specifically on stale paint-buffer pixels.
"""
from pathlib import Path
import subprocess
import tempfile

from fixture_cleanup import cleanup_fixture
from signed_fixture import ROOT, compile_cs

work = Path(tempfile.mkdtemp(prefix='standalone-tests-paint-', dir=ROOT/'build'))
try:
    source = ROOT/'standalone/WsgTheme.cs'
    test = ROOT/'tests/wsg_paint_tests.cs'
    current = work/'current.exe'
    compile_cs(current, test, source)
    passed = subprocess.run([str(current)], capture_output=True, text=True, timeout=30)
    assert passed.returncode == 0, passed.stdout + passed.stderr
    print(passed.stdout.strip())

    text = source.read_text(encoding='utf-8')
    fill = '        using(var background=new SolidBrush(BackColor))e.Graphics.FillRectangle(background,ClientRectangle);'
    assert text.count(fill) == 1, 'Update the negative-control mutation when the renderer changes'
    broken_source = work/'WsgTheme-without-checkbox-background.cs'
    broken_source.write_text(text.replace(fill, ''), encoding='utf-8')
    broken = work/'known-broken.exe'
    compile_cs(broken, test, broken_source)
    failed = subprocess.run([str(broken)], capture_output=True, text=True, timeout=30)
    assert failed.returncode != 0 and 'Checkbox retains stale pixels' in failed.stderr, failed.stdout + failed.stderr
    print('PASS negative control: the v1.0.2.12 repaint defect is rejected')
finally:
    cleanup_fixture(work)
