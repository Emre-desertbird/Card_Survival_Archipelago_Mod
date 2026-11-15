# Working on this repository

Archipelago mod for Card Survival: Tropical Island. Read `docs/plan.md` first: it
holds the design, the current state, the milestones and the open decisions.

## Rules

- **Never write to the player's normal saves** (`%USERPROFILE%\AppData\LocalLow\WinterSpring Games\Card Survival - Tropical Island`)
  and never modify the Steam installation. Only start the game through the dev copy
  in `.local/game`. `tools/dump_game_data.py` shows how to checksum the normal saves
  around a run; do the same for any new tool that starts the game.
- **Never commit game content**: nothing from `.local/` (game copy, dump, decompiled
  code), no assets. `registry.json` (names, ids, rules) is fine.
- `apworld/cardsurvival/registry.json` is generated. Change `tools/build_registry.py`
  and rerun it; do not edit the JSON by hand.
- Logic must come from the dumped game data or the decompiled code, not from memory.
- New gameplay features are optional and off by default.

## Workflow

- Commit directly to `main`. Bump `world_version` in `archipelago.json` and
  `Plugin.Version` together for each published change.
- Test for real where possible: rebuild the plugin, run it in the dev copy, generate
  a real seed (`tools/run_generate.py`), not only unit tests.
- Explain things in simple words, step by step. Propose a plan before large features.
- The playable copy for the user will live in `C:\ProgramData\Archipelago\Card Survival`
  (milestone 5); keep it updated once it exists.

## Useful facts

- Unity 2022.3.62f2, Mono. Game code: `Assembly-CSharp.dll`. Decompile with
  `python tools/setup_dev.py --decompile` into `.local/decompiled`.
- The plugin compiles with C# against the game's own assemblies (`tools/build_plugin.py`).
- The game ships `Newtonsoft.Json` 13.0.2, which Archipelago.MultiClient.Net can use.
- Data objects are `UniqueIDScriptable`s; `UniqueIDScriptable.GetFromID<T>(id)` finds one.
