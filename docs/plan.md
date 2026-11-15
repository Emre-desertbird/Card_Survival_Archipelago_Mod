# Plan

How this mod turns Card Survival: Tropical Island into an Archipelago game, what
already works, and what comes next. Game build `21268943` (version `v1.05ae`).

## Principles (same as the Look Outside mod)

- **Keep the original game.** Hunger, thirst, weather, wounds, combat, cooking and
  exploration are untouched. New features are optional and off by default.
- **Separate modded copy.** The Steam installation stays vanilla. The randomizer
  runs in its own copy with its own save folder.
- **Logic comes from the game's data, not from memory.** A tool reads the real game
  data and builds the list of items and checks. Every release is tied to one game build.
- **Every seed can be finished.** Missable or luck-based checks never hold
  progression. Any real ending wins.
- **Safe saves.** A save belongs to one seed and slot. Checks found offline are sent
  later. Receiving the same item twice never duplicates it.
- **No separate client.** The game connects to the Archipelago server itself.
- **Try it for real.** Real seed generation and in-game test runs, not only unit tests.

## How the game maps to Archipelago

Look Outside is a fixed building with fixed pickups. Card Survival has no fixed
pickups: it is an open survival sandbox, and its real progression is **knowledge**.
You discover a blueprint, spend days researching it, then you can build it.

| Archipelago idea | In this game |
| --- | --- |
| **Check** | Finishing the research of a blueprint ("Research - Kiln"). You spend the research time as usual, but instead of learning that blueprint you send a check. |
| **Item** | A blueprint ("Blueprint: Kiln"). When it arrives, from your own research or from another player, you can build it. |
| **Starting kit** | The nine blueprints every character starts with stay yours: Sharpened Stone, Fire, Campfire, Hand Drill, Rustic Spear, Stone Axe, Broom and both Tourniquets. |
| **Logic** | A blueprint only shows up for research once you hold certain cards ("Clay Pot Cooler needed"). If such a card can only be built, you need its blueprint first. Generation follows these chains. |
| **Goal** | Any real ending: sailing back to civilization, accepting your new life, the Weston ending, or the Mermaid's return to the sea. |
| **Death** | Death ends a run in this game, so DeathLink needs a gentler rule (see open decisions). |

This is the same shape as Factorio or Subnautica in Archipelago. You still survive
day to day, but which tools you get, and when, changes every seed.

Registry 1 contains **200 research checks and 200 blueprint items**. Not shuffled:
the nine starting blueprints, five blueprints that only exist in a character's
starting kit (Ceremonial Dagger, Lizard Drum, Mermaid Nest, Seagull Charm, Shark
Headpiece), two developer test blueprints and the clothing-slot placeholders.

## What works today

| Part | State |
| --- | --- |
| Dev setup (`tools/setup_dev.py`) | Works. Finds the Steam game, makes the modded copy, installs BepInEx 5.4.23.2, fetches the compiler. All downloads are checksum-pinned. |
| Plugin build (`tools/build_plugin.py`) | Works. No .NET SDK or Visual Studio needed. |
| Plugin loads in the game | Verified in the game. |
| Save isolation | Verified. The copy saves to `...\Card Survival - Tropical Island (Archipelago)`. The normal saves were checksum-compared before and after. If the redirect ever fails, the game closes before touching any save. |
| Steam isolation | Verified. The copy starts without Steam and can save. It grants no Steam achievements. |
| Data dump (`tools/dump_game_data.py`) | Works. 1,908 cards, 303 objectives, 361 stats and more, as readable JSON. |
| Registry (`tools/build_registry.py`) | Works. Built from the dump. |
| APWorld | Generates real seeds with Archipelago 0.6.7. 8 unit tests pass. |
| Playing a seed | **Not possible yet.** The plugin does not talk to Archipelago yet. |

## Known limits of registry 1

The logic is deliberately simple for now and too generous in places:

- Cards that no blueprint makes (things you find, harvest, cook or get from animals)
  count as always obtainable. Some of them really need tools or far-away areas.
- Skill requirements ("More Woodworking needed") count as always reachable.
- There are no areas yet: the whole island is one region.
- The goal only requires being able to build the Raft. The real endings need more.

Because of this only 16 of the 200 blueprints are progression so far. Milestone 3
fixes that. Until then, seeds are fine for testing but not for a real race.

## Milestones

1. **Connect.** Add the Archipelago network library to the plugin, a connection
   screen in the game menu, and bind each save to its seed and slot. Queue checks
   while offline.
2. **First playable.** Intercept blueprint research so it sends a check and shows
   what the check holds. Received blueprints become buildable. Detect endings and
   report the goal. Play one small seed start to finish in the dev copy.
   Game hooks found so far: `BlueprintModelsScreen.FinishBlueprintResearch`,
   `GameManager.MakeBlueprintAvailable`, the blueprint branch of `GameManager.AddCard`
   and `EndgameMenu.Setup` (victory and game over both pass through it).
3. **Real logic.** Audit where every card comes from (drops, cooking, exploration,
   animals), which skills and tools a research needs, and how the island's areas
   connect. Audit each ending's real requirements.
4. **Optional extras, all off by default.** Exploration checks (the journal already
   tracks "Explore the Bay" and 18 more areas), journal objectives, survival-day
   checks that only hold filler, supply filler items, DeathLink, an in-game tracker
   and Universal Tracker support.
5. **Release.** An installer that builds the playable copy in
   `C:\ProgramData\Archipelago\Card Survival`, a release ZIP without game files, and
   a full randomized playthrough.

## Open decisions

Each has a default that work continues with until decided otherwise.

1. **DeathLink.** Default: a received death causes a serious injury (bleeding and
   exhaustion) instead of ending the run; your own deaths are sent, except ones
   caused by a received death. A lethal mode could be an option.
2. **Characters.** Default: first support characters that start on the Beach with
   the standard kit (Normal Guy, Survivalist, Tourist, custom characters). Farmer,
   Hunter and Mermaid start elsewhere with different blueprints and come later.
3. **Research with no time cost.** 33 blueprints are learned the moment they are
   discovered. Default: their check is sent at that moment.
4. **Checks beyond blueprints.** Default: exploration and journal checks are
   optional and off, so the base seed is purely about blueprints.
5. **Game name.** The world is called `Card Survival: Tropical Island`. The colon
   means the name must be quoted in a YAML. It cannot change after the first release.
