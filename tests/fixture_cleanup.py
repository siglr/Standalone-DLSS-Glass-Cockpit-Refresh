"""Remove successful disposable fixtures, retaining failures for diagnosis."""
from pathlib import Path
import shutil

def cleanup_fixture(path):
    build = (Path(__file__).resolve().parents[1] / "build").resolve(strict=True)
    target = Path(path).resolve(strict=True)
    if target.parent != build or not target.name.startswith(("installer-tests-", "window-tests-", "standalone-tests-", "update-tests-", "app-update-tests-")):
        raise ValueError(f"Refusing cleanup outside disposable build fixtures: {target}")
    if Path(path).is_symlink() or Path(path).is_junction():
        raise ValueError(f"Refusing linked fixture: {path}")
    if any(item.is_symlink() or item.is_junction() for item in target.rglob("*")):
        raise ValueError(f"Refusing fixture containing links: {target}")
    shutil.rmtree(target)
