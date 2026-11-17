# Card Survival: Tropical Island setup guide

## Requirements

- Windows and Card Survival: Tropical Island (Steam, build `21268943`, version `v1.05ae`).
- Archipelago 0.6.7 or newer.
- BepInEx 5 (x64), 5.4.23 or newer.
- The release zip `CardSurvivalAP-<version>.zip`.

## Installation

Never install the mod into your Steam copy. It runs in a separate copy with its own saves.

1. Copy the game folder (with `Card Survival - Tropical Island.exe`) to a new place, for
   example `C:\ProgramData\Archipelago\Card Survival`.
2. Install BepInEx 5 into that copy: unpack it so `winhttp.dll` sits next to the game's exe.
3. Copy the `BepInEx` folder from the release zip into the copy (merge the folders).
4. Put `cardsurvival.apworld` into Archipelago's `custom_worlds` folder and restart the
   Archipelago launcher.

The copy does not use Steam and grants no achievements. Randomized saves go to
`%USERPROFILE%\AppData\LocalLow\WinterSpring Games\Card Survival - Tropical Island (Archipelago)`;
your normal saves are never read or written.

## Your YAML

```yaml
name: YourName
game: "Card Survival: Tropical Island"
"Card Survival: Tropical Island":
  # Long checks (3+ day research, 12+ hour construction) hold only finds.
  exclude_challenge_checks: true
  # Optional "first time" checks (all off by default).
  fishing_checks: true
  hunting_checks: true
  cooking_checks: true
  farming_checks: true
  # Finds (filler): less / normal / more
  finds_amount: normal
  # Skill items: normal = +25 each (75 of 150), full = +50 each (150 of 150).
  skill_boost: normal
  # New games start as Normal Guy (on by default); the logbook needs it.
  character_lock: true
  logbook: true
  # Perks the base game does not have (off by default).
  new_perks: true
  # Achievements, keeping animals, days survived (off by default).
  extra_checks: true
  # 2 hut upgrade kits and 3 storage extensions, used from F8 inside a hut.
  hut_upgrades: true
  # Mild traps replacing part of the filler (0 = none), and their maximum.
  trap_percentage: 0
  trap_limit: 10
```

The game name contains a colon, so it must be in quotes.

## Playing

1. Start the copy. The **Archipelago** panel is in the main menu.
2. Enter the server (`host:port`), your slot name and the password, then **Connect**.
3. Start a **new game**. With `character_lock` (default) it starts as Normal Guy, the
   game's neutral character, whatever you picked.
4. To continue later, load the save. Connect first or later with **F8**; checks found
   while offline are sent when you connect.

In a game, **F8** opens the panel: connection, chat and commands (`!hint <item>`,
`!release`, ...). Messages about items and checks appear at the bottom left. Finds land on the ground
where you are, at most 10 per in-game hour; the rest waits in the finds buffer of the F8
panel, which lists contents and sender. The research
tab lists what each available research holds and for whom.

## Troubleshooting

- *"This seed was generated with a different APWorld version"*: the apworld and the plugin
  must come from the same release.
- *"This save belongs to ... on seed ..."*: each save is bound to its seed and slot. Start
  a new game for a new seed.
- The log is `BepInEx\LogOutput.log` in the game copy.
