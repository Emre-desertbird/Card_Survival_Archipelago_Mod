# Architecture

How the mod works inside. Two parts share one data file:

```
tools/build_registry.py ──► apworld/cardsurvival/registry.json ◄── read by both
                                   │                     │
                     APWorld (Python, generation)   game plugin (C#, BepInEx, in the game)
```

`registry.json` holds every item and location with its Archipelago id, the game's
`UniqueID` for it, and the logic rules. It is generated from the game data dump; never
edit it by hand. Its `registry_version` is checked when the game connects: a seed made
with another registry is refused with a clear message.

## The APWorld (`apworld/cardsurvival/`)

| File | Role |
| --- | --- |
| `__init__.py` | The world: one region, all locations with their rules, events, item pool, slot data. |
| `access.py` | Turns the JSON rules into Archipelago rules; validates the registry (ids, reachability). |
| `options.py` | `exclude_challenge_checks` (on by default). |
| `registry.json` | Generated data. |

- Item pool: every item with its quantity, then supplies (random kinds) up to the number
  of locations.
- Challenge locations get `LocationProgressType.EXCLUDED` when the option is on.
- Item groups `Blueprints`, `Skills`, `Perks`, `Supplies` and location groups `Research`,
  `Build`, `Visit` work in YAMLs and hint commands.
- Slot data: `registry_version`, the audited game build, the option. Universal Tracker
  can rebuild the world from it (`ut_can_gen_without_yaml`).

## The game plugin (`game_plugin/`)

| File | Role |
| --- | --- |
| `Plugin.cs` | Entry point: settings, registry, Harmony patches, overlay; runs the per-frame loop. |
| `SaveIsolation.cs`, `SteamIsolation.cs` | Own save folder; no Steam (the copy runs without it). |
| `Registry.cs` | Reads `registry.json` (installed next to the DLL). |
| `ApConnection.cs` | The server connection (Archipelago.MultiClient.Net). Network callbacks are queued to the Unity main thread. LocationScouts (no hints created) after connecting. |
| `SlotState.cs` | The per-save Archipelago file (see below). |
| `ApGame.cs` | Binds saves to slots, copies received items into the ledger, applies them, sends checks (and resends after a reconnect). |
| `BlueprintRando.cs` | Splits research from recipes (see below). |
| `Checks.cs` | Build and visit checks. |
| `ItemEffects.cs` | What items do. |
| `SavePatches.cs` | New game, load, save, checkpoint, delete; Safe Mode; ending. |
| `Overlay.cs` | The Archipelago panel (F8), messages, research preview. |
| `SelfTest.cs` | Scripted in-game test, only active when `CSAP_SELFTEST` is set. |

### Save binding: `SlotState`

Each randomized save slot has a file
`...\Card Survival - Tropical Island (Archipelago)\Archipelago\slot_<N>.json` with:

- seed name, slot, team, server, registry version;
- `Received`: every item received, in server order (the ledger);
- `Checked`: every location completed in this save, sent or still waiting;
- `GoalDone`;
- `AppliedMain` and `AppliedCheckpoint` (below).

Starting a new game while connected creates the file; starting one while not connected
makes a normal game. Loading a save with a file reconnects to that seed only: a
connection to another seed or slot is closed with a message. Deleting a save deletes the
file. Writes are atomic (temporary file, then replace).

### Items without doubling

The game keeps two copies of a save: the main data and a checkpoint copy (Safe Mode).
Items have two kinds of effects, chosen by where the game stores the result:

| Kind | Items | How |
| --- | --- | --- |
| Once | supplies, equipment copies, skill levels | Their result is in the game save. `AppliedNow` counts how many received items are already in the running game. `SaveGame` stores it as `AppliedMain` (and `AppliedCheckpoint` when it also writes a checkpoint); `LoadCheckpoint` copies `AppliedCheckpoint` back over `AppliedMain`, exactly like the game copies its checkpoint over its main data. After a rollback, exactly the items received since that checkpoint are applied again. |
| Every session | blueprint recipes, perks | Derived from the ledger each time a game is loaded. The game itself re-applies perk bonuses on every load and does not save them; recipe states are reconciled (below). |

Checks and the goal are never rolled back. The server ignores repeated checks, so
everything is simply resent after every (re)connect.

### Research and recipes: `BlueprintRando`

The game has one state per blueprint (`BlueprintModelStates`: `Available` = buildable,
`Purchasable` = researchable, `Hidden`, `Locked`) and a saved list of researchable
blueprints (`PurchasableBlueprintCards`). The mod reads three facts instead:

| Fact | Source |
| --- | --- |
| research done | the location is in `Checked` (never rolled back) |
| recipe unlocked | the blueprint item is in the ledger |
| discovered | the card exists and, until researched, is in the research list |

`Reconcile` (every 0.25 s and after every relevant event) sets the game state from them:

| | in research list | not in list |
| --- | --- | --- |
| **received** | `Available` (buildable); `Purchasable` while the research tab is open | `Available` |
| **not received** | `Purchasable` (researchable) | `Hidden` |

A received but undiscovered blueprint is added so it can be built, and removed from the
research list: receiving a recipe is not a discovery. Its unlock conditions keep running
(patch on `CardUnlockConditions.IsUnlocked`), so when the vanilla conditions are met it
becomes researchable, and its check stays reachable.

Patched game methods:

| Method | Change |
| --- | --- |
| `BlueprintModelsScreen.FinishBlueprintResearch` | Sends the check instead of teaching (survival basics: both). |
| `GameManager.MakeBlueprintAvailableRoutine` | A vanilla "full unlock" of a shuffled blueprint completes its research instead. |
| `GameManager.AddCard` (blueprint branch, prefix) | Blueprints with no research time send their check when found; a received blueprint found later becomes researchable. |
| `CardUnlockConditions.IsUnlocked` | See above. |
| `BlueprintModelsScreen.ShowSubTab`, `Hide` | Track whether the research tab is open. |
| `BlueprintResearchedPopup.Show` | No "blueprint learned" popup for a recipe that was not received. |

### Checks from playing: `Checks`

- **Build:** the game reports every finished action through `GameManager.OnActionPerformed`
  (its journal uses the same event). Only the final construction step carries the result
  collection `<blueprint>_blueprint`, so earlier steps and AP-given cards never count.
- **Visit:** the current area is compared every frame, so the starting area counts too.

### First-time checks: `Checks.ScanCards`

Fishing, hunting, cooking and farming checks list the cards they wait for (`watch` in the
registry: groups of card ids and how many different ones are needed). A few times per
second the plugin scans the game's cards; every watched card that exists is remembered for
good in `SlotState.Seen`, so "three different crops" also counts crops seen days apart and
checkpoints never undo it. Only kinds switched on in the seed (`location_kinds` from the
slot data, stored with the save) are completed. Finds never contain a watched card; the
registry tool refuses that, so an item can never complete one of these checks.

### The logbook and the character lock

Logbook tasks are watched locations like the first-time checks, with two more kinds of
condition: an action on a card (recorded from `GameManager.OnActionPerformed`, kept in
`SlotState.Actions`) and a number of sent checks of some kinds. After every completed check
the watched locations are evaluated again, so counting tasks follow at once.

A finished task's Determination is a one-time effect: the task goes into `SlotState.Rewards`
(never rolled back), and `ApGame.RewardsNow` counts how many are in the running game. Like
the applied-items counter it is kept for the main save and the checkpoint copy, so after
loading an older checkpoint each reward since then is given exactly once more.

With the logbook on, `JournalRewardPatch` skips the rewards of the normal journal's
objectives (`Objective.OnComplete` for journal objectives), so Determination never doubles.
`CharacterLock` sets `GameManager.CurrentPlayerCharacter` to the Archipelago character
right before `GameLoad.StartNewGame` when the connected seed asks for it.

### New perks: `NewPerks`

A new perk's level is the number of its copies in the received-item ledger (cached), so
like the game's own perks it is derived anew every session. Each acts through one patch:
Pack Mule replaces `GameManager.CalculateCarryWeight` with the same routine and a factor
on the carried sum; Pathfinder and Nimble Hands shorten `CardAction.TotalDaytimeCost` after
`CollectActionModifiers` (travelling actions, the "Build" action), rounded and never under
one tick; Careful Hands adds back part of a negative usage change through the action's
durability changes; Inquisitive Mind scales `GameManager.DaysToTicks` (used for research
only); Pantry Keeper scales a negative `InGameCardBase.CurrentSpoilageRate`.

### Finds: `Finds`

A find is a one-time effect like supplies were. When it is applied it lands on the base row
(the ground) of the current area: plain cards through `GameManager.GiveCard`, liquids the
way the game's own `GiveCard` does it (a coconut shell, then the liquid into it). At most
10 land per in-game hour; the rest go to the buffer (`Finds.Buffer`, indices into the
received-item ledger). The buffer is game progress, so like the applied counter it is kept
twice (`BufferMain`, `BufferCheckpoint`) and follows saves and checkpoint loads. The F8
panel lists the buffer with contents and sender and releases up to 10 at a time.

### Endings, Safe Mode

- `GameLoad.StartNewGame`: Safe Mode is forced on for randomized games.
- `EndgameMenu.Setup` with `_GameOver = false` (a victory) completes the goal. The cheat
  menu's journal view (`_KeepPlaying`) does not. The "Load checkpoint" button is shown on
  the victory screen too.

### Interface: `Overlay`

Unity's immediate-mode GUI, so nothing depends on the game's UI prefabs:

- Main menu: the Archipelago panel (server, slot, password, connect). In game: F8.
- A status line (connection, checks found, goal).
- Messages: items received, checks found, chat and server messages.
- Chat line for messages and commands (`!hint`, `!release`, ...).
- In the research tab, a list of what each available research holds and for whom.

## Tools

| Tool | Does |
| --- | --- |
| `setup_dev.py` | Dev copy, BepInEx, compiler, client library (all pinned), optional decompile. |
| `build_plugin.py` | Compiles the plugin against the game's assemblies; `--install` puts it into the dev copy. |
| `dump_game_data.py` | Starts the dev copy once; the plugin writes all game data as JSON. |
| `card_graph.py` | Every recipe in the game data (see logic.md). |
| `build_registry.py` | Writes `registry.json`. |
| `run_generate.py` | Generates a seed from an Archipelago source checkout. |
| `run_selftest.py` | The in-game self-test against a local server (see testing.md). |
| `package.py` | Release files. |
| `install_playable.py` | The playable copy in `C:\ProgramData\Archipelago\Card Survival`. |
