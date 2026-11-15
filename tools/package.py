"""Build the release files into build/release (no game files are ever included).

  cardsurvival.apworld                 the world, for Archipelago's custom_worlds folder
  CardSurvivalAP-<version>.zip         the apworld plus the game plugin (BepInEx/plugins/...)
                                       and the install instructions

Checks first that the registry is up to date, the unit tests pass and the plugin version
matches world_version in archipelago.json (they are bumped together, see CLAUDE.md).
"""

from __future__ import annotations

import json
import re
import subprocess
import sys
import zipfile
from pathlib import Path

from common import MULTICLIENT_DLL, PROJECT, WORLD

RELEASE = PROJECT / "build" / "release"
PLUGIN_DIR = "BepInEx/plugins/CardSurvivalArchipelago"

INSTALL_TEXT = """Card Survival: Tropical Island - Archipelago {version}

Never install this into your Steam copy of the game. Use a separate copy.

1. Copy the game folder (the one with "Card Survival - Tropical Island.exe") to a new
   place, for example C:\\ProgramData\\Archipelago\\Card Survival.
2. Install BepInEx 5 (x64, 5.4.23 or newer) into that copy:
   https://github.com/BepInEx/BepInEx/releases
3. Copy the "BepInEx" folder from this zip into that copy (merge with the existing one).
4. Put cardsurvival.apworld into Archipelago's custom_worlds folder.
5. Start the copy. The Archipelago panel is in the main menu: enter server, slot and
   password, press Connect, then start a NEW game.

Randomized saves go to their own folder ("... (Archipelago)"); your normal saves are never
used. The copy does not use Steam and grants no achievements.
Full guide: apworld/cardsurvival/docs/setup_en.md in the project.

Includes Archipelago.MultiClient.Net 6.7.0 (MIT license,
https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net).
"""


def main() -> None:
    world_version = json.loads((WORLD / "archipelago.json").read_text(encoding="utf-8"))["world_version"]
    plugin_source = (PROJECT / "game_plugin" / "Plugin.cs").read_text(encoding="utf-8")
    plugin_version = re.search(r'Version = "([^"]+)"', plugin_source).group(1)
    if plugin_version != world_version:
        raise SystemExit(f"Plugin.Version {plugin_version} differs from world_version {world_version}; bump both.")

    subprocess.run([sys.executable, str(PROJECT / "tools" / "build_registry.py"), "--check"], check=True)
    subprocess.run([sys.executable, "-m", "unittest", "discover", "-q", "tests"], cwd=PROJECT, check=True)
    subprocess.run([sys.executable, str(PROJECT / "tools" / "build_plugin.py")], check=True, stdout=subprocess.DEVNULL)

    RELEASE.mkdir(parents=True, exist_ok=True)
    apworld = RELEASE / "cardsurvival.apworld"
    with zipfile.ZipFile(apworld, "w", zipfile.ZIP_DEFLATED) as bundle:
        for path in sorted(WORLD.rglob("*")):
            if path.is_file() and "__pycache__" not in path.parts:
                bundle.write(path, f"cardsurvival/{path.relative_to(WORLD).as_posix()}")

    release = RELEASE / f"CardSurvivalAP-{world_version}.zip"
    with zipfile.ZipFile(release, "w", zipfile.ZIP_DEFLATED) as bundle:
        bundle.write(apworld, apworld.name)
        bundle.write(PROJECT / "build" / "CardSurvivalArchipelago.dll", f"{PLUGIN_DIR}/CardSurvivalArchipelago.dll")
        bundle.write(MULTICLIENT_DLL, f"{PLUGIN_DIR}/{MULTICLIENT_DLL.name}")
        bundle.write(WORLD / "registry.json", f"{PLUGIN_DIR}/registry.json")
        bundle.writestr("INSTALL.txt", INSTALL_TEXT.format(version=world_version))
        bundle.write(PROJECT / "LICENSE", "LICENSE.txt")
    print(json.dumps({"version": world_version, "apworld": str(apworld), "release": str(release),
                      "bytes": release.stat().st_size}, indent=2))


if __name__ == "__main__":
    main()
