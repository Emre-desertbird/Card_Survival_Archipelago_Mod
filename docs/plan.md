# Plan

How this mod turns Card Survival: Tropical Island into an Archipelago game, what
already works, and what comes next. Game build `21268943` (version `v1.05ae`).

Design motto: **randomize progression, preserve survival.** Archipelago items open
up possibilities; checks reward actually using them.

## Principles

- **Keep the original game.** Hunger, thirst, weather, wounds, combat, cooking and
  exploration are untouched. No blanket difficulty reduction. New features are
  optional and off by default.
- **Separate modded copy.** The Steam installation stays vanilla. The randomizer
  runs in its own copy with its own save folder.
- **Logic comes from the game's data, not from memory.** A tool reads the real game
  data and builds the list of items and checks. Every release is tied to one game
  build. Item and location counts follow from the data, never from target numbers.
- **Every seed can be finished, in theory and in practice.** Missable, luck-based or
  very long checks never hold progression or useful items. Any real ending wins.
- **Safe saves.** A save belongs to one seed and slot. Checks found offline are sent
  later. Saving, loading, reconnecting or loading a checkpoint never duplicates an
  item or a check.
- **No separate client.** The game connects to the Archipelago server itself.
- **Try it for real.** Real seed generation and in-game test runs, not only unit tests.

## Hard rules

1. Receiving an AP item never completes a check.
2. Finishing a research and unlocking a recipe are two separate states. Setting one
   never sets the other.
3. A received blueprint never removes or blocks its research check.
4. No progression can be lost forever. One-time items (a finished tool, resources)
   never count as permanent progression unless they can be replaced reliably.
5. No required check can become unreachable forever.
6. Basic survival never depends on AP items (see "Survival basics").
7. Long or unreliable checks hold no important items (Archipelago exclusion).
8. Every alternative vanilla way to get a card is part of the logic.
9. Items and locations are counted from the data; filler makes up the difference.

## How the game maps to Archipelago

Card Survival has no fixed pickups: it is an open survival sandbox, and its real
progression is **knowledge**. You discover a blueprint, spend days researching it,
then you can build it. That makes it close to Factorio or Subnautica in Archipelago.

### Research and recipes (the core)

Each shuffled blueprint has two independent states:

| State | Set by |
| --- | --- |
| `research_completed` | Finishing its research in the research menu. Sends the check "Research - Kiln". Does **not** teach the recipe. |
| `recipe_unlocked` | Receiving the item "Blueprint: Kiln", from yourself or another player. |

- The vanilla conditions for a blueprint to appear for research stay
  ("Clay Pot Cooler needed").
- The research menu shows what the check holds and for whom (LocationScouts).
- A blueprint you already received stays researchable until its check is sent.
  Vanilla hides known blueprints from research, so the mod has to change that.
- 33 blueprints are learned the moment they are discovered (no research time).
  Their check is sent at that moment.

### Survival basics

The nine blueprints every character starts with stay theirs and are not shuffled:
Sharpened Stone, Fire, Campfire, Hand Drill, Rustic Spear, Stone Axe, Broom and both
Tourniquets.

**Gap found in the data:** a shelter, water containers and basic medicine
(`Shelter`, `Waterskin`, `Splint`, ...) are not in that kit; they are normal
researched blueprints. Rule 6 asks for them to be independent of AP items. Default:
a short, data-checked list of **survival blueprints** keeps its vanilla behaviour
(research teaches it as usual) *and* still sends a check. The exact list is decided
in milestone 3 by looking at what an early-game character really needs.

### Items

| Kind | What the player gets | Notes |
| --- | --- | --- |
| **Blueprint** | The recipe, permanently. | Buildings, consumables and intermediate products are not handed out finished. |
| **Equipment blueprint** | The recipe, permanently, plus one finished copy once. | Tools, weapons, clothing. Does not complete the craft or research check. The copy is one-time and never logic. |
| **Progressive skill** | Raises one skill right away (skills run 0-150). | Normal training stays. Never a check. Skills tied to an ending are checked before they are shuffled. |
| **Perk** | A permanent passive bonus. | See below. |
| **Unique card** | A card no blueprint makes. | Never progression if it can be lost. |
| **Filler** | Food, water, building materials, medicine, rare resources, tool replacements. | No traps in the default mode. |

**Perks, as found in the data.** The 89 character perks are creation-screen perks.
Many are negative or are starting scenarios (weather, locations, injuries). The
usable positive ones work in three different ways:

- `PassiveStatModifiers` (for example Slow Metabolism: hunger rate x0.5). These can
  be granted mid-game and are the main perk items.
- `StartingStatModifiers` (the skill perks such as Woodworker give skill levels).
  These overlap with skill items; treat them as skill items, not perks.
- `AddedCards` (the skill perks also add **blueprints**, e.g. Woodworker adds Planks
  and Treenails). This would bypass the randomizer, so perk items never add cards.

New custom perks (less tool wear, faster crafting, ...) come after the vanilla ones,
and must stay noticeable without making a survival mechanic meaningless.

Optional hybrid skill mode (off by default): skills start with a lower cap; each
skill item raises the cap and the level.

### Locations

Every check is one-time and comes only from a real action in the game, never from
receiving an item. AP resources do not count as "gathered yourself"; an AP tool does
not count as "crafted yourself". This follows naturally if checks hook the action,
not the inventory.

| Category | Trigger | Default |
| --- | --- | --- |
| Research | Finish a research | On |
| Crafting | Craft an item for the first time | On |
| Construction | Finish a building or an upgrade | On |
| Exploration | Discover an area or special place (the journal already tracks 19 areas) | On |
| Resources / processing / cooking | First time producing a material or dish | On, list from data |
| Farming, fishing, hunting, animal keeping | First harvest / catch / kill / tame per species | On, rare species are challenge checks |
| Journal | Suitable vanilla objectives | Option |
| Expeditions, events | Special exploration actions and events | Option, luck-based ones are challenge checks |

- **Standard checks** may hold progression and useful items.
- **Challenge checks** (long, rare, luck-based) are excluded from important items.
- **Not implemented:** survival-day counters, skill-level milestones, grinding,
  forced deaths or injuries.

Rough size, to be confirmed by the data: 200 research checks + about as many first
crafts, plus the other categories, for roughly 300-550 checks.

### Areas

All areas stay reachable through their vanilla routes. There are no area-lock items.
The logic still models areas, because an area's checks may need tools, equipment or
skills to be done safely.

### Goal

Any real ending wins (`any_ending`). The game has exactly four victory actions
(found in the card data, `VictorySettings.Victory`):

| Ending | Card | Action |
| --- | --- | --- |
| Escape | `Event_ShipEscape` | Return to civilization |
| Home | `Imp_HomeSign` | Accept your new Life |
| Weston (special ending) | `Event_WestonSpecial5c` | It's working. |
| Mermaid | `Sea_*` (7 sea locations) | Return to the sea |

There is no separate victory action for "Insight". Whether the Weston ending is the
Insight path (Skill_Insight) is checked in milestone 3. The original requirements of
each ending stay. No victory fragments and no simplified raft goal in the final
logic (registry 1 still uses "can build the Raft" as a stand-in).

Victory detection: `GameManager` calls `EndgameMenu.Setup` for a victory action. The
goal is sent there. After the goal the player must be able to keep collecting checks:
the mod adds a "Load checkpoint" button to the victory screen.

### Death and checkpoints

The game already has this built in: **Safe Mode**. With Safe Mode on, every autosave
at a day change also writes a checkpoint, and the game-over screen offers
"Load checkpoint" (`GameLoad.LoadCheckpoint`, `EndgameMenu.SetupCover`). The mod
turns Safe Mode on for every Archipelago save instead of writing its own system.

How items survive a rollback without duplication:

- **Outside the game save** (per seed and slot): checks sent, checks waiting to be
  sent, goal reached. A rollback never takes these back, so a check can't fire twice.
- **Inside the game save**: the index of the last received item that was applied.
  A rollback restores the old index together with the old inventory.
- **On every load** the mod re-applies all permanent items (blueprints, skill items,
  perks) idempotently, then gives one-time items (finished tools, filler) only for
  indexes after the saved one. So a tool lost by a rollback comes back once, and a
  tool still in the save is not given again.

DeathLink: not planned.

### Characters

First, characters that start on the Beach with the standard kit (Normal Guy,
Survivalist, Tourist, custom characters). Farmer, Hunter and Mermaid start elsewhere
with different blueprints and come later; the Mermaid ending needs the Mermaid.
Five blueprints exist only in a character's starting kit and are not shuffled.

## What works today

| Part | State |
| --- | --- |
| Dev setup (`tools/setup_dev.py`) | Works. Finds the Steam game, makes the modded copy, installs BepInEx 5.4.23.2, fetches the compiler. All downloads are checksum-pinned. |
| Plugin build (`tools/build_plugin.py`) | Works. No .NET SDK or Visual Studio needed. |
| Plugin loads in the game | Verified in the game. |
| Save isolation | Verified. The copy saves to `...\Card Survival - Tropical Island (Archipelago)`. If the redirect ever fails, the game closes before touching any save. |
| Steam isolation | Verified. The copy starts without Steam and can save. No Steam achievements. |
| Data dump (`tools/dump_game_data.py`) | Works. 1,908 cards, 303 objectives, 361 stats, 89 perks and more, as JSON. |
| Registry (`tools/build_registry.py`) | Works. 200 research checks and 200 blueprint items. |
| APWorld | Generates real seeds with Archipelago 0.6.7. 8 unit tests pass. |
| Playing a seed | **Not possible yet.** The plugin does not talk to Archipelago yet. |

Known limits of registry 1: cards no blueprint makes count as always obtainable,
skill requirements count as reachable, the island is one region, and the goal is
"can build the Raft". Only 16 of 200 blueprints are progression so far.

## Milestones

The order puts a small playable slice first, so the risky game hooks are proven
before the large data work. Each step is tested in the dev copy.

1. **Connect.** Archipelago.MultiClient.Net in the plugin, a connection screen in the
   game menu, each save bound to its seed and slot, checks queued while offline,
   the received-item ledger.
2. **First playable (research only).** Research sends a check and shows what it
   holds; received blueprints become buildable; known blueprints stay researchable;
   Safe Mode forced on and rollback re-application; victory sends the goal; a
   checkpoint button after victory. Play one small seed start to finish.
   Hooks found so far: `BlueprintModelsScreen.FinishBlueprintResearch`,
   `GameManager.MakeBlueprintAvailable`, the blueprint branch of `GameManager.AddCard`,
   `EndgameMenu.Setup`, `GameLoad.LoadCheckpoint`.
3. **Real logic.** Where every card comes from (drops, cooking, exploration,
   animals), which tools, skills and areas each research and craft needs, how the
   areas connect, each ending's real requirements, the survival-blueprint list.
   Automated check: every generated seed is beatable with this logic.
4. **More checks.** Crafting, construction, exploration, resources, cooking,
   processing, farming, fishing, hunting, animals; then journal, expeditions and
   events as options. Challenge-check classification.
5. **More items.** Equipment copies, progressive skills, perks, unique cards, filler.
   Optional hybrid skill caps.
6. **Release.** An installer that builds the playable copy in
   `C:\ProgramData\Archipelago\Card Survival`, a release ZIP without game files, an
   in-game tracker / Universal Tracker support, and full multiworld playthroughs for
   balancing.

## Open decisions

Each has a default that work continues with until decided otherwise.

1. **Survival blueprints.** Default: a short list (shelter, water container, basic
   medicine) keeps vanilla research behaviour and also sends a check. List fixed in
   milestone 3.
2. **Research with no time cost.** Default: the check is sent on discovery.
3. **Skill items per skill.** Default: a few steps per skill (e.g. +25 each), count
   set so skills still need some training. Balanced in milestone 6.
4. **Game name.** The world is called `Card Survival: Tropical Island`. The colon
   means the name must be quoted in a YAML. It cannot change after the first release.
