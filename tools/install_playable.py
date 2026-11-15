"""Install or update the playable copy for this machine (default C:\\ProgramData\\Archipelago\\Card Survival).

1. Copies the Steam installation there once (the Steam copy is only read, never changed).
2. Installs BepInEx 5 (checksum-pinned) into the copy.
3. Installs the current plugin, the Archipelago client library and registry.json.
4. Writes "Play Card Survival (Archipelago).bat" next to the game.
5. Puts cardsurvival.apworld into Archipelago's custom_worlds folder (skip with --no-apworld).

Run tools/package.py first. Safe to run again to update; saves are not touched (randomized
saves live in "...\\Card Survival - Tropical Island (Archipelago)", never in the normal folder).
"""

from __future__ import annotations

import argparse
import json
import shutil
from pathlib import Path

from common import BEPINEX, GAME_EXE, MULTICLIENT_DLL, PROJECT, TOOLS, WORLD, extract, fetch, find_steam_game

DEFAULT_TARGET = Path(r"C:\ProgramData\Archipelago\Card Survival")
CUSTOM_WORLDS = Path(r"C:\ProgramData\Archipelago\custom_worlds")
LAUNCHER = """@echo off
rem Starts the Archipelago copy of Card Survival: Tropical Island.
rem It does not use Steam; randomized saves go to their own "(Archipelago)" folder.
cd /d "%~dp0"
start "" "%~dp0{exe}"
"""


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--target", type=Path, default=DEFAULT_TARGET)
    parser.add_argument("--game-dir", type=Path, help="Game installation to copy (default: found through Steam)")
    parser.add_argument("--no-apworld", action="store_true", help="Do not copy the apworld into custom_worlds")
    args = parser.parse_args()

    plugin = PROJECT / "build" / "CardSurvivalArchipelago.dll"
    apworld = PROJECT / "build" / "release" / "cardsurvival.apworld"
    if not plugin.is_file() or not apworld.is_file():
        raise SystemExit("Run tools/package.py first.")

    steam = find_steam_game()
    target = args.target
    copied = False
    if not (target / GAME_EXE).is_file():
        source = args.game_dir or (Path(steam["path"]) if steam["installed"] else None)
        if source is None or not (source / GAME_EXE).is_file():
            raise SystemExit("Card Survival: Tropical Island was not found. Install it in Steam or pass --game-dir.")
        if source.resolve() == target.resolve():
            raise SystemExit("The target must not be the game installation itself.")
        shutil.copytree(source, target, ignore=shutil.ignore_patterns("BepInEx", "doorstop_config.ini", "winhttp.dll"))
        copied = True
    if not (target / "BepInEx" / "core" / "BepInEx.dll").is_file():
        extract(fetch(BEPINEX, TOOLS / "bepinex"), target)

    plugins = target / "BepInEx" / "plugins" / "CardSurvivalArchipelago"
    plugins.mkdir(parents=True, exist_ok=True)
    shutil.copy2(plugin, plugins / plugin.name)
    shutil.copy2(MULTICLIENT_DLL, plugins / MULTICLIENT_DLL.name)
    shutil.copy2(WORLD / "registry.json", plugins / "registry.json")
    (target / "Play Card Survival (Archipelago).bat").write_text(LAUNCHER.format(exe=GAME_EXE), encoding="ascii")

    installed_world = None
    if not args.no_apworld and CUSTOM_WORLDS.is_dir():
        installed_world = CUSTOM_WORLDS / apworld.name
        shutil.copy2(apworld, installed_world)

    version = json.loads((WORLD / "archipelago.json").read_text(encoding="utf-8"))["world_version"]
    print(json.dumps({"target": str(target), "game_copied_now": copied, "version": version,
                      "steam_build": steam.get("build_id"), "build_matches_audit": steam.get("build_matches"),
                      "apworld": str(installed_world) if installed_world else None}, indent=2))


if __name__ == "__main__":
    main()
