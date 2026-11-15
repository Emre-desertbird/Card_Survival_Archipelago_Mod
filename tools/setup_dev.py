"""Prepare the development environment. Safe to run again at any time.

1. Finds the Steam installation (read-only, never modified).
2. Copies it to .local/game, the separate modded copy used for development.
3. Installs BepInEx 5 into that copy.
4. Downloads the C# compiler used by tools/build_plugin.py and the Archipelago
   client library (Archipelago.MultiClient.Net) the plugin is built against.
With --decompile it also decompiles the game code into .local/decompiled for reading.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
from pathlib import Path

from common import (BEPINEX, DATA_DIR, DEV_GAME, GAME_EXE, ILSPY, LOCAL, MOD_SAVES, MULTICLIENT, MULTICLIENT_DLL,
                    ROSLYN, TOOLS,
                    extract, fetch, find_steam_game)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--game-dir", type=Path, help="Game installation (default: found through Steam)")
    parser.add_argument("--decompile", action="store_true", help="Also decompile Assembly-CSharp (needs dotnet 8)")
    args = parser.parse_args()

    steam = find_steam_game()
    source = args.game_dir or (Path(steam["path"]) if steam["installed"] else None)
    if source is None or not (source / GAME_EXE).is_file():
        raise SystemExit("Card Survival: Tropical Island was not found. Install it in Steam or pass --game-dir.")

    if not (DEV_GAME / GAME_EXE).is_file():
        shutil.copytree(source, DEV_GAME, ignore=shutil.ignore_patterns("BepInEx", "doorstop_config.ini", "winhttp.dll"))
    if not (DEV_GAME / "BepInEx" / "core" / "BepInEx.dll").is_file():
        extract(fetch(BEPINEX, TOOLS / "bepinex"), DEV_GAME)
    compiler = TOOLS / "roslyn" / "pkg" / "tasks" / "net472" / "csc.exe"
    if not compiler.is_file():
        extract(fetch(ROSLYN, TOOLS / "roslyn"), TOOLS / "roslyn" / "pkg")

    if not MULTICLIENT_DLL.is_file():
        extract(fetch(MULTICLIENT, TOOLS / "multiclient"), TOOLS / "multiclient" / "pkg")

    decompiled = LOCAL / "decompiled" / "Assembly-CSharp"
    if args.decompile and not any(decompiled.glob("*.cs")):
        extract(fetch(ILSPY, TOOLS / "ilspycmd"), TOOLS / "ilspycmd" / "pkg")
        tool = TOOLS / "ilspycmd" / "pkg" / "tools" / "net8.0" / "any" / "ilspycmd.dll"
        decompiled.mkdir(parents=True, exist_ok=True)
        subprocess.run(["dotnet", str(tool), "-p", "-o", str(decompiled),
                        str(DEV_GAME / DATA_DIR / "Managed" / "Assembly-CSharp.dll")], check=True)

    print(json.dumps({"steam": steam, "dev_game": str(DEV_GAME), "dev_saves": str(MOD_SAVES), "compiler": str(compiler),
                      "decompiled": decompiled.is_dir()}, indent=2))


if __name__ == "__main__":
    main()
