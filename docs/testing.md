# Testing

Three levels, from fast to real. Run all three before publishing a change.

## 1. Unit tests (seconds, no game, no Archipelago)

```powershell
python -m unittest discover tests
```

They check the rule parser, that the registry is valid (unique ids, every location
reachable with all items, the goal needs progress), that the vanilla order has no
deadlock, the survival basics, the skill totals and that challenge checks leave room for
every important item.

## 2. Real seed generation

From an Archipelago source checkout (Python 3.11-3.13):

```powershell
python tools/run_generate.py --ap-src <Archipelago source>                                  # one player
python tools/run_generate.py --ap-src <Archipelago source> --players tests/fixtures_multi   # three players
```

With an installed Archipelago, after `tools/install_playable.py` put the apworld into
`custom_worlds`:

```powershell
& "C:\ProgramData\Archipelago\ArchipelagoGenerate.exe" --player_files_path tests\fixtures_multi --outputpath .local\gen
```

Generation fails if a seed cannot be finished. The spoiler's playthrough shows the chain
to the goal.

## 3. In-game self-test (a few minutes)

```powershell
python tools/run_selftest.py --ap-src <Archipelago source> --python <python 3.13>
python tools/run_selftest.py ... --game-dir "C:\ProgramData\Archipelago\Card Survival"   # the installed copy
```

It generates a solo seed, hosts it on a local Archipelago server, and starts the game with
`CSAP_SELFTEST`. The plugin then plays a scripted session and writes a report. Saves go to
`...(Archipelago Test)`; the tool checksums the normal saves before and after and stops if
they changed. 37 steps at the moment:

- connect, scout all locations, bind a new save, Safe Mode on, starting area check;
- discover a blueprint (researchable, not buildable), research it (check sent and
  confirmed by the server, recipe not learned);
- the item from that check arrives: recipe buildable, its own research still open;
  vanilla discovery opens it; research tab and build tabs both work;
- a survival basic teaches and sends its check;
- a build check fires only on the final step;
- a skill (+25), a perk and an equipment copy arrive;
- after reloading: nothing lost, nothing doubled, ledger equals the server;
- after loading an older checkpoint: checks kept, items since then applied exactly once;
- an ending completes the goal (the server log is checked) and offers the checkpoint.

## Still manual

- A full playthrough of a multiworld seed (building, travelling, sailing for real).
- The look of the overlay at other resolutions.
