"""Run VS 2022 build tools without assuming a particular edition/install drive."""
from pathlib import Path
import os
import shutil
import subprocess
import sys


def find_cmake():
    override = os.environ.get("UVR_CMAKE")
    if override:
        candidate = Path(override)
        if not candidate.is_file():
            raise SystemExit(f"UVR_CMAKE does not name an executable: {candidate}")
        return candidate
    vswhere = Path(os.environ.get("ProgramFiles(x86)", "C:/Program Files (x86)")) / "Microsoft Visual Studio/Installer/vswhere.exe"
    if vswhere.is_file():
        found = subprocess.check_output([
            str(vswhere), "-latest", "-products", "*", "-version", "[17.0,18.0)",
            "-requires", "Microsoft.VisualStudio.Component.VC.Tools.x86.x64",
            "-find", "Common7/IDE/CommonExtensions/Microsoft/CMake/CMake/bin/cmake.exe",
        ], text=True).strip().splitlines()
        if found:
            return Path(found[0])
    found = shutil.which("cmake")
    if found:
        return Path(found)
    raise SystemExit("CMake not found. Install VS 2022 C++ Build Tools with CMake and Windows SDK 10.0.26100.0, or set UVR_CMAKE.")


def main(args):
    root = Path(__file__).resolve().parents[1]
    env = {key.upper(): value for key, value in os.environ.items()}
    env["MSBUILDDISABLENODEREUSE"] = "1"
    if not args:
        args = ["-S", str(root), "-B", str(root / "build/standalone"), "-G", "Visual Studio 17 2022", "-A", "x64"]
    exe = find_cmake()
    if args[0] == "ctest":
        exe = exe.with_name("ctest.exe")
        args = args[1:]
    if not exe.is_file():
        raise SystemExit(f"Build tool missing: {exe}")
    return subprocess.call([str(exe), *args], env=env)


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
