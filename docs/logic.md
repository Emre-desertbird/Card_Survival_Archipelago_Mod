# Logic

How generation decides what is reachable. Everything comes from the game data dump
(`tools/card_graph.py`, `tools/build_registry.py`); nothing is typed in from memory except
the short lists named at the end.

## Step 1: every recipe

A *recipe* is one way to get cards. It lists what it needs and what it produces. The tool
reads them from:

| Source in the data | Needs | Produces |
| --- | --- | --- |
| Actions on a card (`DismantleActions`) | the card, required cards/tags on the board, stats | drops, transformations |
| Travelling (paths are actions) | the path card | the area card it leads to |
| Dragging one card onto another (`CardInteractions`) | both cards (tool by card or tag) | drops, both transformations, liquids |
| Cooking (`CookingRecipes`) | the container and an ingredient | results, changed ingredient or cooker |
| Time (spoilage, usage, fuel, progress, special durabilities at 0 or full) | the card | drops, transformation |
| Stat-triggered changes, destruction, generated or contained liquids, starting inventories | the card | cards, liquids |
| Exploring (`ExplorationResults`), arriving in an area (`DefaultEnvCards`) | the card / area | what you find |
| Fights (`Encounters`, via `DroppedEncounter`) | the action | everything the fight can drop |
| Self-triggered events (`SelfTriggeredAction`) | required cards, stat triggers | cards |
| Improvements in an area (`EnvironmentImprovements`) | the area, building materials, unlock conditions | the improvement |
| Building a blueprint (`BlueprintResult`) | "Can Build X" | the result |

Two kinds of **pseudo-cards** use the same machinery:

- `stat:<Stat>`: the stat can be raised. Produced by every action, effect or card that
  changes the stat upwards (with that action's own needs). A requirement such as
  "Swimming ≥ 20" or "Distance ≥ 336" needs it, but only if the stat starts below the
  required value. Stats that rise by themselves count from the start; stats that need a
  character perk are never reachable.
- `victory:<card>`: that ending can be played (its victory action's needs are met).

"Can Build X" needs the blueprint item (unless X is a starting blueprint or a survival
basic), every building material of every stage (any card of a material group), required
liquids and the non-inverted build conditions.

## Step 2: what is free

A fixed point starting from the game mode's start (the Beach, its base cards and weather)
with only the starting blueprints and survival basics: 732 cards (plus 211 raisable stats)
are reachable without any Archipelago item. In rules they are simply `true`. A second
fixed point with every item shows what is reachable at all; recipes that can never work
are dropped.

## Step 3: rules

Every card that is neither free nor impossible becomes an event `Have <card>`, whose rule is
"any of its recipes, each needing all of its needs". Blueprints become `Can Build <name>`.
Only events something depends on are kept (440 at the moment).

| Location | Rule |
| --- | --- |
| Research - X | the conditions that reveal X in vanilla (`CardUnlockConditions`: conditions sharing an objective name are alternatives, different names must all hold; cards, tags, raised stats) |
| Build - X | `Can Build X` |
| Visit - Area | `Have <area>` |
| Catch, Hunt, Cook - X | `Have` any card of the group |
| Plant - N different crops | `{"atleast": N, "of": [Have crop, ...]}` |
| Goal | `Can Play Ending Event_ShipEscape` |

An item is **progression** when a check or the goal depends on it, directly or through
events; every blueprint is, because its own "Build" check needs it. Skills, perks and
supplies are useful or filler: the logic never relies on them (training raises skills).

Example chain found by the tool: Blueprint: Raft → Can Build Raft → the raft entrance →
the raft area → Distance can be raised → the rescue events → Escape.

## Known limits (deliberate approximations)

- **How far a stat can rise** is not modelled: "can be raised" counts as enough. This is
  why the logic does not use the Home ending (comfort 1751+, 120 days, determination).
- **Drop chances**: any chance counts. A rare drop is still "reachable".
- **Durability ranges** (e.g. a tool must have charges left) are ignored.
- **Areas**: a card you have once counts as available anywhere, and a fixed card in one
  area counts as reachable from everywhere. Areas are only checked through travelling.
- **One-time cards**: a unique card that can be used up still counts as available.
- **Characters**: the logic starts at the Beach. Character kits only add things.
- Unlock conditions on time and on completed journal objectives count as reachable.

Because of these, a seed can expect something that needs more effort than the logic
says. All of them err on the generous side for rare cases only; the main chains (tools,
materials, buildings, areas, sailing) are modelled. Report a case where the logic expects
something impossible; the fix belongs in `card_graph.py`.

## Hand-made lists (the only ones)

In `tools/build_registry.py`, each with its reason in a comment:

- `SURVIVAL_BASICS`: six blueprints kept as in vanilla (rule 6).
- `GOAL_ENDING`: the ending the logic guarantees.
- `EXCLUDED_SKILLS`: Insight (tied to endings).
- `FINDS`, `FIND_SHARES`: what finds contain and their share of the filler pool.
- `FISHING`, `HUNTING`: which cards count as a catch or a carcass of each kind (cooking
  and farming come from the game's own tags).
- `CHALLENGE_RESEARCH_DAYS`, `CHALLENGE_BUILD_HOURS`: thresholds for challenge checks.
- `SKILL_COPIES`, `SKILL_STEP`: 3 × 25, matching the game's skill perks (+75).
