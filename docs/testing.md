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
they changed. 73 steps at the moment (the test seed switches every option on, traps at 100 % up to 10):

- connect, scout all locations, bind a new save, the character lock starts Normal Guy
  (the test picks another character), Safe Mode on, starting area check;
- discover a blueprint (researchable, not buildable), research it (check sent and
  confirmed by the server, recipe not learned);
- the item from that check arrives: recipe buildable, its own research still open;
  vanilla discovery opens it; research tab and build tabs both work;
- a survival basic teaches and sends its check;
- a build check fires only on the final step;
- a skill (+25), a perk and an equipment copy arrive;
- the seed's options are stored with the save; a watched card completes its first-time
  check; "three different crops" needs the third crop;
- logbook: a card task, an action task (needs both actions), a task counting sent checks;
  Determination given once, the journal's reward for the same thing skipped;
- the journal book opens on the logbook: six chapter pages, done tasks ticked (a
  screenshot `journal.png` is saved next to the report);
- all ten new perk levels arrive through real checks; each effect is measured (carried
  weight x0.7, 30 min travel -> 15, 15 stays 15, 45 min building -> 30, tool wear -30 %,
  research ticks -30 %, spoilage x0.85); levels are kept after reload;
- 14 finds at once: 10 land on the ground (a liquid one as a filled coconut shell), the
  rest waits in the buffer; taking one from the buffer works;
- after reloading: the game finishes loading (events the game shows on load, such as a
  skill level reached, are confirmed like a player would), nothing lost, nothing doubled,
  ledger equals the server, the buffer is as saved;
- after loading an older checkpoint: checks kept, items since then applied exactly once,
  finds since then land again or wait in the buffer without duplicates;
- extras: keeping partridges completes its check, "survive 7 days" does not on day 1;
- traps: two arrive, at most one goes off per day, the other waits;
- a shed is built and entered, a storage extension adds 25 %, a stone is put inside, the
  upgrade kit turns it into a mud hut (stone and extension kept), outside the entrance is
  a mud hut;
- the character screen's diary and the cheat journal do not complete the goal;
- an ending completes the goal (the server log is checked) and offers the checkpoint.

## Still manual

- A full playthrough of a multiworld seed (building, travelling, sailing for real).
- The look of the overlay at other resolutions.
