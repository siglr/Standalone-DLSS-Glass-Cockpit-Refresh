"""Extract the actual module scanner for an offline API-shim regression test."""
from pathlib import Path
import sys
text = Path(sys.argv[1]).read_text()
start = text.index('void scan_modules() {')
end = text.index('\n}\n', start) + 3
Path(sys.argv[2]).write_text(text[start:end])
