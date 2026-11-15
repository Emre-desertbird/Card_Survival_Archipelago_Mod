# Card Survival Archipelago Mod

An [Archipelago](https://archipelago.gg) integration for **Card Survival: Tropical
Island**, in early development.

> **Status: not playable yet.** Seeds can be generated and the game plugin loads,
> but the plugin does not connect to Archipelago yet. This is a vibe-coded project
> written with Claude Code. See the [plan](docs/plan.md) for what works and what is next.

## The idea

Blueprints are shuffled. You still discover a blueprint and spend the time
researching it, but finishing the research **sends a check** instead of teaching
you that blueprint. Blueprints **arrive as items**, from your own research or from
another player's world. Everything else about surviving on the island stays as it is.

- 200 research checks and 200 blueprint items.
- The nine starting blueprints (Stone Axe, Campfire, …) stay available.
- Any real ending completes your goal.
- The randomizer runs in a **separate copy** of the game with its own save folder.
  Your Steam installation and your normal saves are never changed.

## Requirements

- Windows, Card Survival: Tropical Island on Steam, build `21268943` (`v1.05ae`).
- Python 3.10 or newer for the tools.
- Archipelago 0.6.7 or newer to generate seeds.

## Development setup

```powershell
python tools/setup_dev.py              # copy the game to .local/game, install BepInEx, fetch the compiler
python tools/build_plugin.py --install # compile game_plugin/ and put it into the copy
python tools/dump_game_data.py         # start the copy once; it writes the game data and closes
python tools/build_registry.py         # rebuild apworld/cardsurvival/registry.json from that data
python -m unittest discover tests      # registry and rule tests
```

To generate a real seed you need an Archipelago source checkout and a Python
version it supports (3.11 to 3.13):

```powershell
python tools/run_generate.py --ap-src <path to Archipelago source>
```

Everything under `.local/` (the game copy, downloads, the data dump, decompiled
code) stays on your machine and is never committed.

## Layout

| Folder | Contents |
| --- | --- |
| `apworld/cardsurvival/` | The Archipelago world. `registry.json` is generated from game data. |
| `game_plugin/` | The BepInEx plugin (C#) that runs inside the game. |
| `tools/` | Setup, build, data dump and registry tools. |
| `tests/` | Unit tests and sample YAML. |
| `docs/` | The [plan](docs/plan.md). |

## Credits and license

- **Card Survival: Tropical Island** belongs to WinterSpring Games. No game files are included.
- [Archipelago](https://github.com/ArchipelagoMW/Archipelago) is the multiworld framework,
  [BepInEx](https://github.com/BepInEx/BepInEx) the plugin loader.
- The approach follows the
  [Look Outside Archipelago Mod](https://github.com/wyldek/Look_Outside_Archipelago_Mod) by wyldek.

Released under the [MIT License](LICENSE).
