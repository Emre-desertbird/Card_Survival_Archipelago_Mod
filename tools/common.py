"""Paths and helpers shared by the development tools."""

from __future__ import annotations

import hashlib
import re
import urllib.request
import zipfile
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
LOCAL = PROJECT / ".local"            # ignored by git: game copy, tools, dumps
DEV_GAME = LOCAL / "game"             # separate modded copy used for development
TOOLS = LOCAL / "tools"
DUMP = LOCAL / "dump"                 # raw game data written by the plugin
WORLD = PROJECT / "apworld" / "cardsurvival"

STEAM_APP_ID = "1694420"
GAME_EXE = "Card Survival - Tropical Island.exe"
DATA_DIR = "Card Survival - Tropical Island_Data"
AUDITED_BUILD_ID = "21268943"         # Steam build the registry was audited against

# Pinned downloads (name, url, sha256). Checked before anything is extracted.
BEPINEX = ("BepInEx_win_x64_5.4.23.2.zip",
           "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.2/BepInEx_win_x64_5.4.23.2.zip",
           "f752ce4e838f4c305b9da1404b6745f2cff23b8bfd494f79f0c84d0a01f59b46")
ROSLYN = ("Microsoft.Net.Compilers.Toolset.4.8.0.zip",
          "https://www.nuget.org/api/v2/package/Microsoft.Net.Compilers.Toolset/4.8.0",
          "37333f4f1e2ce55e621355d6da651dc23d4cb5f94a8f76b9478816e87f110ad9")
# Archipelago client library (MIT). The net45 build uses the game's own Newtonsoft.Json.
MULTICLIENT = ("Archipelago.MultiClient.Net.6.7.0.zip",
               "https://www.nuget.org/api/v2/package/Archipelago.MultiClient.Net/6.7.0",
               "b31023610626c524126099a3974588f8a3e1d117f36d375895ea59b7964acdc8")
MULTICLIENT_DLL = TOOLS / "multiclient" / "pkg" / "lib" / "net45" / "Archipelago.MultiClient.Net.dll"
ILSPY = ("ilspycmd.9.1.0.7988.zip",
         "https://www.nuget.org/api/v2/package/ilspycmd/9.1.0.7988",
         "2b5058f5ccc164c33b7aabf1a5eb0cf3d3a6af6c145aaf58efd3ed891443af7c")


LOCAL_LOW = Path.home() / "AppData" / "LocalLow" / "WinterSpring Games"
REAL_SAVES = LOCAL_LOW / "Card Survival - Tropical Island"                    # never written by the mod
MOD_SAVES = LOCAL_LOW / "Card Survival - Tropical Island (Archipelago)"       # see SaveIsolation.cs


def save_fingerprint() -> dict[str, str]:
    """Checksums of the player's normal save files, to prove a test run left them alone."""
    files = [path for path in REAL_SAVES.rglob("*") if path.is_file() and "Unity" not in path.relative_to(REAL_SAVES).parts
             and not path.name.startswith("Player")] if REAL_SAVES.is_dir() else []
    return {str(path.relative_to(REAL_SAVES)): hashlib.sha256(path.read_bytes()).hexdigest() for path in sorted(files)}


def steam_libraries() -> list[Path]:
    roots = [Path(r"C:\Program Files (x86)\Steam"), Path(r"C:\Program Files\Steam")]
    libraries: list[Path] = []
    for root in roots:
        index = root / "steamapps" / "libraryfolders.vdf"
        if not index.is_file():
            continue
        for match in re.finditer(r'"path"\s+"([^"]+)"', index.read_text(encoding="utf-8", errors="replace")):
            library = Path(match.group(1).replace("\\\\", "\\"))
            if library not in libraries:
                libraries.append(library)
    return libraries


def find_steam_game() -> dict:
    """Locate the Steam installation and report its build id."""
    for library in steam_libraries():
        manifest = library / "steamapps" / f"appmanifest_{STEAM_APP_ID}.acf"
        if not manifest.is_file():
            continue
        text = manifest.read_text(encoding="utf-8", errors="replace")
        value = lambda key: (re.search(rf'"{key}"\s+"([^"]*)"', text) or [None, ""])[1]
        path = library / "steamapps" / "common" / value("installdir")
        return {"installed": (path / GAME_EXE).is_file(), "path": str(path), "build_id": value("buildid"),
                "audited_build_id": AUDITED_BUILD_ID, "build_matches": value("buildid") == AUDITED_BUILD_ID}
    return {"installed": False, "path": None, "build_id": None,
            "audited_build_id": AUDITED_BUILD_ID, "build_matches": False}


def fetch(pinned: tuple[str, str, str], folder: Path) -> Path:
    """Download a pinned archive into its own folder and verify its checksum."""
    name, url, sha256 = pinned
    folder.mkdir(parents=True, exist_ok=True)
    target = folder / name
    if not target.is_file():
        request = urllib.request.Request(url, headers={"User-Agent": "nuget-client"})
        with urllib.request.urlopen(request) as response:
            target.write_bytes(response.read())
    actual = hashlib.sha256(target.read_bytes()).hexdigest()
    if actual != sha256:
        target.unlink()
        raise SystemExit(f"Checksum mismatch for {name}: got {actual}")
    return target


def extract(archive: Path, folder: Path) -> None:
    folder.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as bundle:
        for member in bundle.namelist():
            if not (folder / member).resolve().is_relative_to(folder.resolve()):
                raise SystemExit(f"Unsafe path in {archive.name}: {member}")
        bundle.extractall(folder)
