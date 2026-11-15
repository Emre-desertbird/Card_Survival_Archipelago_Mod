"""Compile game_plugin/*.cs into the BepInEx plugin DLL.

Uses the pinned Roslyn compiler from tools/setup_dev.py and compiles against the
game's own assemblies, so no .NET SDK or Visual Studio is needed.
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
from pathlib import Path

from common import DATA_DIR, DEV_GAME, MULTICLIENT_DLL, PROJECT, TOOLS, WORLD

PLUGIN_NAME = "CardSurvivalArchipelago"


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-dir", type=Path, default=DEV_GAME, help="Modded game copy with BepInEx")
    parser.add_argument("--install", action="store_true", help="Copy the DLL into that copy's BepInEx/plugins")
    args = parser.parse_args()

    compiler = TOOLS / "roslyn" / "pkg" / "tasks" / "net472" / "csc.exe"
    managed = args.game_dir / DATA_DIR / "Managed"
    core = args.game_dir / "BepInEx" / "core"
    if not compiler.is_file() or not managed.is_dir() or not core.is_dir() or not MULTICLIENT_DLL.is_file():
        raise SystemExit("Run tools/setup_dev.py first.")

    references = sorted(managed.glob("*.dll")) + [core / "BepInEx.dll", core / "0Harmony.dll", MULTICLIENT_DLL]
    sources = sorted((PROJECT / "game_plugin").rglob("*.cs"))
    output = PROJECT / "build" / f"{PLUGIN_NAME}.dll"
    output.parent.mkdir(exist_ok=True)
    response = output.with_suffix(".rsp")
    response.write_text("\n".join(
        ["-nologo", "-nostdlib+", "-target:library", "-optimize+", "-debug-", "-deterministic+",
         "-langversion:latest", "-nowarn:CS1701,CS1702", f'-out:"{output}"'] +
        [f'-r:"{path}"' for path in references] + [f'"{path}"' for path in sources]), encoding="utf-8")
    # -noconfig must be on the command line: it is ignored inside a response file.
    subprocess.run([str(compiler), "-noconfig", f"@{response}"], check=True)

    installed = None
    if args.install:
        installed = args.game_dir / "BepInEx" / "plugins" / PLUGIN_NAME / output.name
        installed.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(output, installed)
        # The client library and the id registry ship next to the plugin. The game's own
        # Newtonsoft.Json is used, so the package's copy is not installed.
        shutil.copy2(MULTICLIENT_DLL, installed.parent / MULTICLIENT_DLL.name)
        shutil.copy2(WORLD / "registry.json", installed.parent / "registry.json")
    print(json.dumps({"plugin": str(output), "bytes": output.stat().st_size,
                      "installed": str(installed) if installed else None}))


if __name__ == "__main__":
    main()
