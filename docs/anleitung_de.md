# Anleitung: Card Survival mit Archipelago spielen

## Was ist anders als im normalen Spiel?

Du überlebst auf der Insel wie gewohnt. Hunger, Durst, Wetter, Wunden, Kochen und Kämpfe
bleiben unverändert. Gemischt wird dein **Wissen**:

- **Forschung schickt einen Check.** Wenn eine Forschung fertig ist, lernst du den Blueprint
  *nicht*. Stattdessen geht ein Gegenstand an dich oder an einen anderen Spieler.
- **Blueprints kommen als Items.** Wenn du „Blueprint: Kupferaxt“ erhältst, kannst du sie
  sofort bauen. Ihre Forschung bleibt trotzdem offen und ist ein eigener Check.
- **Weitere Checks:** einen Blueprint zum ersten Mal fertig bauen („Build - …“) und ein
  Gebiet zum ersten Mal betreten („Visit - …“).
- **Optionale Checks** (in der YAML einschalten): der erste Fang jeder Fischart
  („Catch - …“), der erste Kadaver jedes Tiers („Hunt - …“), jedes Topf- und
  Kokosschalen-Gericht einmal („Cook - …“) und ein paar Anbau-Meilensteine („Plant - …“).
- **Weitere Items:** Skill-Stufen (+25) und hilfreiche Perks aus dem Spiel.
  Ausrüstungs-Blueprints bringen einmalig ein fertiges Exemplar mit.
- **Fundstücke** (Füll-Items): „Ein mysteriöses Wesen hat etwas zurückgelassen.“ Lange
  haltbares Essen, Getränke in einer Kokosschale, Kochzutaten oder einfaches Material
  landen auf dem Boden des Gebiets, in dem du gerade bist. Pro Spielstunde landen höchstens
  10. Der Rest wartet im **Zwischenspeicher** (F8-Fenster). Dort siehst du Inhalt und
  Absender und holst die Sachen per Knopf ab.

Immer verfügbar sind die neun Start-Blueprints (Steinaxt, Lagerfeuer …) und die
**Grundversorgung**: Shelter, Leaf Bed, Torch, Clay Bowl, Splint und Ash Dressing. Diese
sechs erforschst du wie im normalen Spiel, und dabei schicken sie zusätzlich ihren Check.

## Charakter und Logbuch

Archipelago-Spiele starten immer als **Normal Guy**: keine Perks, keine Startausrüstung,
Start am Strand. Die Logik ist auf ihn zugeschnitten. Wählst du im Menü einen anderen
Charakter, startet das Spiel trotzdem als Normal Guy; das Archipelago-Fenster sagt das an.
(Abschaltbar mit `character_lock: false`.)

Mit `logbook: true` bekommt Normal Guy das **Archipelago-Logbuch**: 44 Aufgaben in sechs
Kapiteln (Gestrandet, Überleben, Die Insel, Handwerk, Multiworld, Aufbruch). Jede Aufgabe
ist ein Check und gibt Determination wie das normale Journal, dessen Belohnungen sie
ersetzt. Zusammen reicht das für das Home-Ending. Das Logbuch steht im
**Journal-Buch** des Spiels (eine Seite pro Kapitel, mit Häkchen und Fortschritt) und
zusätzlich im F8-Fenster unter „Logbook“.

## Extras, Hütten-Upgrades und Fallen

- **Extras** (`extra_checks`): 11 Spielerfolge, die kein anderer Check abdeckt (z. B. fünf
  Figuren schnitzen, ein Schiffswrack finden, ein Ei ausbrüten), Tiere halten (Ziege,
  Rebhühner, Wildschwein) und 7/14/30/60/100 Tage überleben.
- **Hütten-Upgrades** (`hut_upgrades`): 2 Ausbau-Sets und 3 Stauraum-Erweiterungen. Du setzt
  sie im F8-Fenster ein, während du **in** einer Hütte stehst. Das Ausbau-Set macht aus dem
  Schuppen eine Lehmhütte bzw. aus der Lehmhütte eine Steinhütte; alles darin bleibt. Dafür
  speichert die Mod kurz und lädt neu. Es geht, wenn du genau eine Hütte dieser Art und noch
  keine der nächsten hast. Die Stauraum-Erweiterung gibt der Hütte oder dem Keller 25 % mehr
  Platz.
- **Fallen** (`trap_percentage`, `trap_limit`): harmlos, das Schlimmste ist eine leichte
  Wunde. Höchstens eine pro Spieltag, keine auf See, im Kampf oder während eines
  Ereignisses. Der Rest wartet (das F8-Fenster zeigt, wie viele).

## Neue Perks (Option `new_perks`)

Perks, die es im Spiel nicht gibt, in kleinen Stufen:

| Perk | Wirkung |
| --- | --- |
| Progressive Pack Mule (3 Stufen) | Getragenes Gewicht zählt je Stufe 10 % weniger. |
| Nimble Hands | Bauen von Blueprints 33 % schneller. |
| Pathfinder | Reisen 33 % schneller. |
| Progressive Careful Hands (2 Stufen) | Werkzeuge nutzen sich je Stufe 15 % langsamer ab. |
| Progressive Inquisitive Mind (2 Stufen) | Forschung je Stufe 15 % schneller. |
| Pantry Keeper | Essen und Getränke verderben 15 % langsamer. |

Zeiten rechnet das Spiel in 15-Minuten-Schritten. Deshalb wird gerundet, und nichts wird
kürzer als 15 Minuten: Eine 15-Minuten-Reise bleibt 15 Minuten. Das F8-Fenster zeigt die
aktiven neuen Perks.

## Ziel

Jedes echte Ende zählt: Rettung auf See, „Accept your new Life“, das Weston-Ende oder die
Rückkehr der Meerjungfrau ins Meer. Die Logik stellt sicher, dass die **Rettung auf See**
erreichbar ist (Floß bauen, hinaussegeln, gerettet werden). Nach dem Ende kannst du den
letzten Checkpoint laden und weiter Checks sammeln.

## Tod

Randomizer-Spiele laufen immer im **Safe Mode** des Spiels. Bei jedem Tageswechsel wird ein
Checkpoint gespeichert. Stirbst du, lädst du den Checkpoint. Gefundene Checks und
erhaltene Items bleiben erhalten. Was du seit dem Checkpoint bekommen hast, wird genau
einmal wieder hinzugefügt.

## Einrichten (auf diesem PC schon erledigt)

Die spielbare Kopie liegt in `C:\ProgramData\Archipelago\Card Survival`. Die APWorld liegt
in `C:\ProgramData\Archipelago\custom_worlds\cardsurvival.apworld`. Neu einrichten oder
aktualisieren:

```powershell
python tools/package.py
python tools/install_playable.py
```

Deine Steam-Installation und deine normalen Spielstände werden nie verändert.
Randomizer-Spielstände liegen im eigenen Ordner
`...\WinterSpring Games\Card Survival - Tropical Island (Archipelago)`.

## Spielen

1. Eine YAML anlegen, zum Beispiel in `C:\ProgramData\Archipelago\Players`:

   ```yaml
   name: DeinName
   game: "Card Survival: Tropical Island"
   "Card Survival: Tropical Island":
     exclude_challenge_checks: true
     fishing_checks: true      # optional, Standard: aus
     hunting_checks: true
     cooking_checks: true
     farming_checks: true
     finds_amount: normal      # less / normal / more
     logbook: true             # optional, Standard: aus
     new_perks: true           # optional, Standard: aus
     extra_checks: true        # optional, Standard: aus
     hut_upgrades: true        # optional, Standard: aus
     trap_percentage: 0        # 0 bis 100, Standard 0 = keine Fallen
     trap_limit: 10            # höchstens so viele Fallen
   ```

   Der Spielname enthält einen Doppelpunkt und muss deshalb in Anführungszeichen stehen.
2. Mit dem Archipelago-Launcher generieren und hosten (oder auf archipelago.gg hochladen).
3. **„Play Card Survival (Archipelago).bat“** im Ordner der Kopie starten.
4. Im Hauptmenü erscheint links das Fenster **Archipelago**. Dort Server (`host:port`),
   Slot-Name und Passwort eintragen und **Connect** drücken.
5. Ein **neues Spiel** starten. Damit gehört der Spielstand fest zu diesem Seed.
6. Zum Weiterspielen den Spielstand laden und verbinden. Das geht auch später im Spiel mit
   **F8**. Was du offline gefunden hast, wird beim Verbinden nachgeschickt.

Im Spiel:

- **F8** öffnet und schließt das Archipelago-Fenster. Dort gibt es auch den Chat und die
  Befehle (`!hint Blueprint: Raft`, `!release` …).
- Unten links erscheinen Meldungen: was du erhalten hast und welcher Check was enthielt.
- Im **Forschungs-Tab** zeigt eine Liste, was jede verfügbare Forschung enthält und für wen.

## Wenn etwas nicht klappt

- *„different APWorld version“*: APWorld und Mod müssen aus derselben Version stammen.
  Dann `tools/install_playable.py` erneut ausführen.
- *„This save belongs to …“*: Jeder Spielstand gehört zu genau einem Seed und Slot.
  Für einen neuen Seed ein neues Spiel starten.
- Das Log liegt in `C:\ProgramData\Archipelago\Card Survival\BepInEx\LogOutput.log`.
