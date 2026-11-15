# Plan

How this mod turns Card Survival: Tropical Island into an Archipelago game: the design,
what works today, what comes next and the open decisions. Game build `21268943`
(version `v1.05ae`).

Design motto: **randomize progression, preserve survival.** Archipelago items open up
possibilities; checks reward actually using them.

Related documents:

| Document | For |
| --- | --- |
| [architecture.md](architecture.md) | How the plugin and the APWorld work inside, and why. |
| [logic.md](logic.md) | How the logic is derived from the game data, and its known limits. |
| [testing.md](testing.md) | Unit tests, seed generation and the in-game self-test. |
| [anleitung_de.md](anleitung_de.md) | Player guide in German (install, connect, play). |
| [../apworld/cardsurvival/docs/](../apworld/cardsurvival/docs/) | Game page and setup guide shown by Archipelago. |

## Principles

- **Keep the original game.** Hunger, thirst, weather, wounds, combat, cooking and
  exploration are untouched. No blanket difficulty reduction. New features are optional
  and off by default.
- **Separate modded copy.** The Steam installation stays vanilla. The randomizer runs in
  its own copy with its own save folder.
- **Logic comes from the game's data, not from memory.** A tool reads the real game data
  and builds the items, checks and rules. Every release is tied to one game build.
- **Every seed can be finished**, in theory and in practice. Long checks hold no
  important items. Any real ending wins.
- **Safe saves.** A save belongs to one seed and slot. Checks found offline are sent later.
  Saving, loading, reconnecting or loading a checkpoint never doubles an item or a check.
- **No separate client.** The game connects to the Archipelago server itself.
- **Try it for real.** Real seed generation and in-game test runs, not only unit tests.

## Hard rules

1. Receiving an AP item never completes a check.
2. Finishing a research and unlocking a recipe are separate states.
3. A received blueprint never removes or blocks its research check.
4. No progression can be lost forever; one-time items are never logic.
5. No required check can become unreachable forever.
6. Basic survival never depends on AP items.
7. Long checks hold no important items (Archipelago exclusion).
8. Alternative vanilla ways to learn a blueprint are respected.
9. Items and locations are counted from the data; filler makes up the difference.

## What the randomizer does

### Locations (451, up to 538 with the optional ones)

| Kind | Count | Sent when |
| --- | --- | --- |
| Research - X | 200 | The research of blueprint X finishes (or X is learned in another vanilla way). |
| Build - X | 209 | Blueprint X is finished for the first time (crafted or constructed). |
| Visit - Area | 42 | You enter that area for the first time. |
| Catch - X (option `fishing_checks`) | 11 | The first fish or seafood of a kind exists. |
| Hunt - X (option `hunting_checks`) | 10 | The first carcass of an animal exists. |
| Cook - X (option `cooking_checks`) | 17 | A cooking pot or coconut shell dish exists for the first time. |
| Plant - ... (option `farming_checks`) | 5 | First crop, three and six different crops, first tree, first mushroom bed. |
| Logbook - ... (option `logbook`) | 44 | A task of the Archipelago logbook is done (see below). |

The optional kinds are off by default. Kinds nothing can reach in this game mode (Grouper,
Seahound) are dropped by the registry tool.

8 long checks (research of 3+ days, construction of 12+ hours) are **challenge checks**:
by default they only hold filler (`exclude_challenge_checks`, on by default).

### Items

| Kind | Count | Effect |
| --- | --- | --- |
| Blueprint: X | 194 | Recipe X, permanently. 53 equipment blueprints also give one finished copy. |
| Progressive Skill | 66 (22 skills × 3) | +25 on that skill (3 × 25 = the game's own skill perk). Insight is excluded. |
| Perk: X | 11 | A helpful vanilla perk with passive bonuses only (e.g. Slow Metabolism). |
| Find: X | the rest (180 to 223) | Filler: long-lasting food, drinks, cooking ingredients or common materials, left on the ground where you are (see below). |

**Finds** come in five categories with fixed shares of the filler pool: material 35 %,
ingredients 20 %, provisions 20 %, drinks 15 %, treats 10 %. Only things the base game
already keeps for a long time; nothing valuable (no copper, rope, cloth, tools); liquids only
in a coconut shell. At most 10 finds land per in-game hour; the rest waits in a **buffer**
in the F8 panel (contents and sender listed, taken on demand). `finds_amount`:
`less` (half of them are worthless keepsakes), `normal`, `more` (50 % larger amounts).

### The Archipelago character and the logbook

`character_lock` (on by default): every new Archipelago game starts as **Normal Guy**, the
game's neutral character (no perks, no kit, the Beach). He is the base model of custom
characters and not in the character list, so the start is redirected to him; the F8 panel
says so in the main menu. The logic is made for him.

`logbook` (off by default, needs the character lock): 44 new tasks in six chapters
(Stranded, Survival, The Island, Craft, Multiworld, Departure). Each task is a check and
gives Determination like the normal journal, whose rewards it replaces (the journal still
shows its tasks). Together 5,300 Determination, at least 4,000 with only the base check
kinds, so the Home ending (4,000) stays possible. Tasks never ask to *receive* items; the
Multiworld chapter counts checks you sent (e.g. 10 fishing checks, 50/100/200 checks).
The logbook is a page of the F8 panel.

### Not shuffled

- The nine starting blueprints: Broom, Campfire, Fire, Hand Drill, Rustic Spear,
  Sharpened Stone, Stone Axe and both Tourniquets.
- **Survival basics** (rule 6): Shelter, Leaf Bed, Torch, Clay Bowl, Splint, Ash Dressing.
  Their research teaches the recipe as in vanilla *and* sends its check. They have no item.
- Five blueprints that only exist in a character's starting kit, and two test blueprints.

### Goal

Any real ending completes the goal: Escape (rescue at sea), Home ("Accept your new
Life"), the Weston ending, or the Mermaid's return to the sea. These are the only four
victory actions in the game data. The **logic** guarantees the Escape ending, the only one
fully described by cards (raft, sailing, rescue); see [logic.md](logic.md). After the
ending screen, "Load checkpoint" lets you keep collecting checks.

### Death

Randomized games always use the game's own **Safe Mode**: every day change writes a
checkpoint, and dying offers "Load checkpoint". The Archipelago progress (checks, items,
goal) is never rolled back; see [architecture.md](architecture.md). No DeathLink.

## Current state (version 0.5.0)

| Part | State |
| --- | --- |
| Dev setup, plugin build, data dump | Work. All downloads checksum-pinned. |
| Save and Steam isolation | Verified; normal saves are checksum-compared around every test run. |
| Connection (Archipelago.MultiClient.Net 6.7.0) | Works: panel in the main menu and in game (F8), chat and commands (!hint). |
| Save binding, offline queue, reconnect | Works and is tested. |
| Research split, blueprint items, survival basics | Work, tested in the real game. |
| Build and visit checks | Work. The build hook is tested with the game's own action report; a full manual playthrough is still due. |
| Skills, perks, equipment copies | Work, tested including reload and checkpoint. |
| Finds, hourly limit, buffer | Work, tested in the real game: ground row, filled coconut shells, buffer across reload and checkpoint. |
| Fishing, hunting, cooking, farming checks | Work, tested in the real game (first-time and counting checks). |
| Character lock, logbook | Work, tested in the real game: Normal Guy start, card, action and check-counting tasks, Determination exactly once across reload and checkpoint, journal rewards skipped. The logbook is shown in the F8 panel, not yet in the game's journal book. |
| Ending detection, goal, checkpoint after victory | Work, tested. |
| Logic from data | 440 events from 4,573 recipes; all 194 blueprint items are progression. |
| APWorld | Generates with Archipelago 0.6.7 (source) and the installed 0.6.8; 19 unit tests. |
| Playable copy | Installed in `C:\ProgramData\Archipelago\Card Survival` (tools/install_playable.py). |
| Release | `tools/package.py` builds `cardsurvival.apworld` and a release zip. |

## Milestones

1. **Connect.** Done.
2. **First playable (research).** Done.
3. **Real logic from data.** Done (see logic.md for what is approximated).
4. **More checks.** Build and visit: done. Fishing, hunting, cooking, farming: done (0.4.0,
   optional). Archipelago logbook and character lock: done (0.5.0). Next: new perks
   ([plan_erweiterung.md](plan_erweiterung.md)).
5. **More items.** Skills, perks, equipment copies, supplies: done. Next, optional:
   hybrid skill caps, new custom perks (less tool wear, faster crafting).
6. **Release.** Packaging and installer: done. Still due: a full manual playthrough of a
   multiworld seed, then balancing (skill step size, supplies, challenge thresholds).

## Open decisions

Each has a default that work continues with until decided otherwise.

1. **Survival basics list.** Default: the six listed above.
2. **Logic goal.** Default: Escape. Home could be added once comfort can be modelled.
3. **Characters.** Default: the logic starts at the Beach (the game mode's start). Hunter
   and Farmer start elsewhere but share the island; a perk start (Plane Crash) has no visit
   check. The Mermaid ending works but the logic never relies on it.
4. **More check categories.** Default: off until each has a reliable hook and a data list.
   The proposal (checks and matching items, with numbers from the dump) is in
   [plan_erweiterung.md](plan_erweiterung.md).
5. **Game name.** `Card Survival: Tropical Island`. The colon means the name must be
   quoted in a YAML. It cannot change after the first public release.
