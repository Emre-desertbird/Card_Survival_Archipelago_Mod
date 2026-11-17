# Card Survival: Tropical Island

## What does randomization do to this game?

You survive on the island as usual, but your **knowledge** is shuffled.

- Finishing a blueprint's research sends a check instead of teaching you the blueprint.
- Blueprints arrive as items, from your own checks or from other players. A received
  blueprint can be built right away, and its research stays open as a check of its own.
- Finishing a blueprint for the first time and entering an area for the first time are
  checks too.
- Optional checks: the first catch of each fish, the first carcass of each animal, each
  cooking pot dish, and a few farming milestones.
- Other items: progressive skills (+25 each, +50 with `skill_boost: full`) and helpful perks of the base game. With
  `new_perks`, also perks the game does not have, in small steps: carry more, build and
  travel faster, tools wear slower, research faster, food spoils slower.
  Equipment blueprints also come with one finished copy.
- Optional: extra checks (achievements, keeping animals, days survived), hut upgrade kits
  and storage extensions, and mild traps (a light wound at worst, one per day).
- Filler items are **finds**: long-lasting food, drinks, cooking ingredients or common
  materials that appear on the ground where you are (at most 10 per in-game hour; the rest
  waits in a buffer you open with F8).

## Which character?

Archipelago games start as Normal Guy: no perks, no starting kit, the Beach. With the
`logbook` option he keeps an Archipelago logbook: 44 tasks that are checks and give the
Determination the normal journal would give. The logbook is part of the game's journal book.

## What stays the same?

Survival itself: hunger, thirst, weather, wounds, exploration, cooking and combat are
unchanged. The nine starting blueprints stay yours. Shelter, Leaf Bed, Torch, Clay Bowl,
Splint and Ash Dressing are researched as in the base game (and also send a check), so
basic survival never waits for other players.

## What is the goal?

Any real ending of the game: rescue at sea, accepting your new life, the Weston ending
or the Mermaid's return to the sea. The logic guarantees the rescue at sea. After the
ending you can load the last checkpoint and keep collecting checks.

## What happens when I die?

Randomized games always use the game's Safe Mode: you go back to the last checkpoint
(written every morning). Checks you found and items you received are kept.

## Which checks can hold important items?

All except a few long ones (research of 3+ days, construction of 12+ hours), which hold
only finds unless you turn off `exclude_challenge_checks`.

## Item and location groups

Items: `Blueprints`, `Skills`, `Perks`, `New Perks`, `Finds`. Locations: `Research`, `Build`, `Visit`,
`Fishing`, `Hunting`, `Cooking`, `Farming`, `Logbook`, `Extras`. Also `Hut Upgrades` and
`Traps` for items.
