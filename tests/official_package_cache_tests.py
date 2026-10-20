"""A release-pin update must not silently reuse another release's cached files."""
from pathlib import Path
from unittest.mock import patch
import hashlib
import io
import sys
import tempfile
from fixture_cleanup import cleanup_fixture
from signed_fixture import ROOT

sys.path.insert(0,str(ROOT/'scripts'))
from official_test_package import fetch

work=Path(tempfile.mkdtemp(prefix='standalone-tests-upstream-cache-',dir=ROOT/'build'))
try:
    cached=work/'official.zip'
    cached.write_bytes(b'previous release')
    current=b'current official release'
    expected=hashlib.sha256(current).hexdigest()
    with patch('urllib.request.urlopen',return_value=io.BytesIO(current)) as download:
        fetch('https://example.invalid/new-release',cached,expected_hash=expected)
        assert cached.read_bytes()==current and download.call_count==1
    with patch('urllib.request.urlopen',side_effect=AssertionError('Unnecessary download')):
        fetch('https://example.invalid/new-release',cached,expected_hash=expected)
    with patch('urllib.request.urlopen',return_value=io.BytesIO(b'wrong content')):
        try:fetch('https://example.invalid/next-release',cached,expected_hash='f'*64)
        except RuntimeError:pass
        else:raise AssertionError('Incorrect official checksum accepted')
        assert cached.read_bytes()==current, 'Failed download replaced the verified cache'
    source=work/'source.zip';source.write_bytes(b'old commit')
    source.with_name('source.zip.url').write_text('https://example.invalid/old-commit')
    with patch('urllib.request.urlopen',return_value=io.BytesIO(b'new commit')) as download:
        fetch('https://example.invalid/new-commit',source)
        assert source.read_bytes()==b'new commit' and download.call_count==1
    with patch('urllib.request.urlopen',side_effect=AssertionError('Unnecessary download')):
        fetch('https://example.invalid/new-commit',source)
    print('PASS changed release/source pins refresh caches; matching caches reuse; invalid downloads preserve verified bytes')
finally:
    cleanup_fixture(work)
