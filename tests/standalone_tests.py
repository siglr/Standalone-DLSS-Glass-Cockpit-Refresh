"""Compatibility entry point for the current native standalone installer suite."""
import runpy
from pathlib import Path
runpy.run_path(str(Path(__file__).with_name("native_installer_tests.py")),run_name="__main__")
