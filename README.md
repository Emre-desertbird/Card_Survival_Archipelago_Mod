# Card Survival Archipelago Mod

An [Archipelago](https://archipelago.gg) integration for **Card Survival: Tropical
Island**, in early development.

> **Status: playable development build (0.3.0).** Connecting, research, building,
> exploring, items, checkpoints and endings work and are tested in the real game; a full
> manual multiworld playthrough is still due. This is a vibe-coded project written with
> Claude Code. See the [plan](docs/plan.md).

## The idea

You survive on the island as usual, but your **knowledge** is shuffled. Finishing a
research **sends a check** instead of teaching you that blueprint; blueprints **arrive as
items**, from your own checks or from other players. Building a blueprint for the first
time and entering an area for the first time are checks too.

- 451 checks: 200 research, 209 first builds, 42 areas.
- Items: 194 blueprints (equipment comes with one finished copy), 66 skill levels,
  11 perks of the base game, supplies.
- Starting blueprints and six survival basics (shelter, bed, torch, bowl, splint,
  dressing) never wait for other players.
- Any real ending completes your goal. Death loads the game's own checkpoint.
- The logic is derived from the game data ([how](docs/logic.md)).
- The randomizer runs in a **separate copy** of the game with its own save folder.
  Your Steam installation and your normal saves are never changed.

## Playing

See the [setup guide](apworld/cardsurvival/docs/setup_en.md) (German:
[Anleitung](docs/anleitung_de.md)).

## Requirements for development

- Windows, Card Survival: Tropical Island on Steam, build `21268943` (`v1.05ae`).
- Python 3.10 or newer for the tools; dotnet 8 only for decompiling.
- Archipelago 0.6.7 or newer (a source checkout with Python 3.11-3.13 for the test tools).

## Development

```powershell
python tools/setup_dev.py              # dev copy in .local/game, BepInEx, compiler, client library
python tools/build_plugin.py --install # compile game_plugin/ into the dev copy
python tools/dump_game_data.py         # start the copy once; it writes the game data and closes
python tools/build_registry.py         # rebuild apworld/cardsurvival/registry.json from that data
python -m unittest discover tests      # unit tests
python tools/run_generate.py --ap-src <Archipelago source>        # a real seed
python tools/run_selftest.py --ap-src <Archipelago source>        # scripted test in the real game
python tools/package.py                # release files in build/release
python tools/install_playable.py       # playable copy in C:\ProgramData\Archipelago\Card Survival
```

Everything under `.local/` (the game copy, downloads, the data dump, decompiled code)
stays on your machine and is never committed. Details: [architecture](docs/architecture.md),
[testing](docs/testing.md).

## Layout

| Folder | Contents |
| --- | --- |
| `apworld/cardsurvival/` | The Archipelago world. `registry.json` is generated from game data. |
| `game_plugin/` | The BepInEx plugin (C#) that runs inside the game. |
| `tools/` | Setup, build, data, logic, test and release tools. |
| `tests/` | Unit tests and sample YAMLs. |
| `docs/` | Plan, architecture, logic, testing, German guide. |

## Credits and license

- **Card Survival: Tropical Island** belongs to WinterSpring Games. No game files are included.
- [Archipelago](https://github.com/ArchipelagoMW/Archipelago) is the multiworld framework,
  [BepInEx](https://github.com/BepInEx/BepInEx) the plugin loader and
  [Archipelago.MultiClient.Net](https://github.com/ArchipelagoMW/Archipelago.MultiClient.Net)
  (MIT) the client library.
- The approach follows the
  [Look Outside Archipelago Mod](https://github.com/wyldek/Look_Outside_Archipelago_Mod) by wyldek.

Released under the [MIT License](LICENSE).
