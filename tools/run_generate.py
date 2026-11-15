"""Generate a real Archipelago seed with the working APWorld, from an Archipelago source checkout.

The world is copied into the checkout's worlds/ folder for the run and removed
again afterwards, so the checkout stays clean for other projects.
Usage: run_generate.py --ap-src <Archipelago source> [--players tests/fixtures] [--seed 1]
"""

from __future__ import annotations

import argparse
import os
import shutil
import subprocess
import sys
from pathlib import Path

from common import LOCAL, PROJECT, WORLD


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--ap-src", type=Path, default=os.environ.get("AP_SRC"), required="AP_SRC" not in os.environ)
    parser.add_argument("--players", type=Path, default=PROJECT / "tests" / "fixtures")
    parser.add_argument("--seed", default="1")
    args, extra = parser.parse_known_args()   # anything else is passed on to Generate.py

    source = Path(args.ap_src).resolve(strict=True)
    target = source / "worlds" / WORLD.name
    output = LOCAL / "ap-output"
    output.mkdir(parents=True, exist_ok=True)
    shutil.rmtree(target, ignore_errors=True)
    shutil.copytree(WORLD, target, ignore=shutil.ignore_patterns("__pycache__"))
    try:
        result = subprocess.run(
            [sys.executable, "-B", "Generate.py", "--seed", str(args.seed), "--player_files_path",
             str(args.players.resolve()), "--outputpath", str(output), *extra],
            cwd=source, env=dict(os.environ, SKIP_REQUIREMENTS_UPDATE="1"))
    finally:
        shutil.rmtree(target, ignore_errors=True)
    raise SystemExit(result.returncode)


if __name__ == "__main__":
    main()
