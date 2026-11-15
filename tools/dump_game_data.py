"""Start the modded dev copy once so the plugin writes all game data to .local/dump.

The game opens, reaches the main menu, writes one JSON file per data type and
closes by itself. The dump is local-only input for the audit tools; it contains
game data and is never committed.
"""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
from pathlib import Path

from common import DEV_GAME, DUMP, GAME_EXE, save_fingerprint


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, default=DEV_GAME)
    parser.add_argument("--timeout", type=int, default=300, help="Seconds to wait for the game to finish")
    args = parser.parse_args()

    shutil.rmtree(DUMP, ignore_errors=True)
    DUMP.mkdir(parents=True)
    environment = dict(os.environ, CSAP_DUMP_DIR=str(DUMP))
    before = save_fingerprint()
    process = subprocess.Popen([str(args.game_dir / GAME_EXE), "-logFile", str(DUMP / "Player.log"),
                                "-screen-fullscreen", "0",
                                "-screen-width", "1280", "-screen-height", "720"],
                               cwd=args.game_dir, env=environment)
    try:
        process.wait(timeout=args.timeout)
    except subprocess.TimeoutExpired:
        process.kill()
        raise SystemExit("The game did not finish dumping in time; see BepInEx/LogOutput.log in the dev copy.")
    if save_fingerprint() != before:
        raise SystemExit("STOP: the normal save files changed during this run. Save isolation is broken.")
    manifest = DUMP / "manifest.json"
    if not manifest.is_file():
        error = DUMP / "error.txt"
        raise SystemExit(error.read_text(encoding="utf-8") if error.is_file() else
                         "No dump was written; see BepInEx/LogOutput.log in the dev copy.")
    data = json.loads(manifest.read_text(encoding="utf-8"))
    size = sum(path.stat().st_size for path in DUMP.glob("*.json"))
    if not data.get("saves_isolated"):
        raise SystemExit(f"STOP: the modded copy is not using its own save folder ({data.get('save_folder')}).")
    print(json.dumps({"dump": str(DUMP), "megabytes": round(size / 1e6, 1),
                      "normal_saves_untouched": True, **data}, indent=2))


if __name__ == "__main__":
    main()
