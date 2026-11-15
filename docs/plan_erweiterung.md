# Plan: neue Checks und neue Items

Status: **Vorschlag, noch nicht gebaut.** Alle Zahlen stammen aus dem Daten-Dump
(`.local/dump`) und aus `tools/card_graph.py`, nicht aus dem Gedächtnis.

- „frei“ heißt: ohne ein einziges Archipelago-Item erreichbar.
- „Items“ heißt: erst mit Items erreichbar.
- „nie“ heißt: mit diesem Spielmodus gar nicht erreichbar (z. B. nur als Meerjungfrau).
  Solche Checks lässt das Werkzeug automatisch weg.

## 1. Grundidee

Fast alle neuen Checks folgen dem Muster **„zum ersten Mal X“**. Dafür reicht **ein**
gemeinsamer Mechanismus statt einer eigenen Lösung pro Kategorie:

| Art | Wann der Check zählt | Haken im Spiel | Logik |
| --- | --- | --- | --- |
| `obtain` | Karte X entsteht zum ersten Mal | `GameManager.AddCard` (schon gepatcht) | `Have X` aus dem Kartengraphen (gibt es schon) |
| `objective` | ein Spielziel ist erfüllt | `Objective.OnComplete` | Bedingungen des Ziels: Karten, Tags, Werte, Aktionen |
| `stat` | ein Wert erreicht eine Schwelle | Abfrage wie beim Visit-Check | `Can Raise <Wert>` |

Wichtige Regeln dabei:

- **Karten, die die Mod selbst gibt, zählen nicht.** Sonst würde ein Vorratspaket mit
  gebratenem Fisch sofort den Koch-Check auslösen. Die Mod markiert ihre eigenen Karten.
- **Karten aus der Startausrüstung zählen nicht.** Das betrifft Startinventar und Charakterkit.
- **Jede Kategorie ist eine eigene YAML-Option und standardmäßig aus.**
- **Die Belohnungen des Spiels bleiben.** Journal-Ziele geben im Spiel *Determination*.
  Das brauchen die Endings, also schickt die Mod den Check **zusätzlich**.

## 2. Neue Checks

### 2.1 Fischen: „Catch - X“ (13 Checks)

Der erste Fang jeder Art.

- **Frei (12):** Goatfish, Parrot Fish, Bonefish, Mudskipper, King Threadfin, Shark Meat,
  Prawns, Conch, Oyster, Urchin. Dazu Bonefish- und Threadfin-Fleisch; vielleicht
  zusammenlegen.
- **Mit Items (1):** Herring, nur mit der Floß-Fischfalle.
- **Nie:** Grouper, fällt weg.

Fast alles ist frei. Das ist gut für einen frühen Start in Multiworlds.

### 2.2 Jagd: „Hunt - X“ (10 bis 15 Checks)

Der erste Kadaver je Tierart, genau wie bei den Jagd-Zielen des Spiels
(`ObjH5_Hunt*` prüft auch auf den Kadaver).

| Status | Tiere |
| --- | --- |
| frei | Wildschwein, Kobra, Ziegenbock, Makak, Waran, Rebhuhn, Seeschlange, Möwe, Hai |
| Items | Ferkel, Ziege (weiblich), Zicklein (brauchen Gehege oder Tiere halten) |
| nie | Seehund (nur Meerjungfrau) |

Vorschlag: eine Tierart = ein Check, also 10. Varianten wie die drei Rebhuhn-Karten
zählen gemeinsam.

### 2.3 Kochen: „Cook - X“ (17 bis 74 Checks)

Das erste Mal, dass ein Gericht entsteht.

- **Gerichte (17):** Topf- und Kokosschalen-Gerichte wie Yam Curry, Goat Stew,
  Hearty Feast oder Seafood Cup. Alle brauchen Items, meist den Kochtopf.
- **Alles Gegarte (74):** dazu gebraten, geräuchert, getrocknet und gekocht.
  32 davon sind frei, 42 brauchen Items, 6 sind „nie“ und fallen weg.

Vorschlag für die Option `cooking_checks: off / meals / all`.

### 2.4 Anbau: „Plant - X“ (25 Checks)

Das erste Beet jeder Pflanze: 22 Pflanzenbeete und 3 Pilzbeete. Alle brauchen Items,
weil das Beet ein Blueprint ist. Damit verteilen sich gute Items auch in die spätere
Spielphase.

### 2.5 Erkunden: „Explore - Gebiet“ (19 Checks)

Ein Gebiet vollständig erkundet, also der Erkundungsbalken ist voll.

- Das ist mehr als „Visit“ (nur betreten).
- Das Spiel hat dafür 19 Ziele (`ObjExplore_*`).
- Diese Ziele laufen im Spiel nur im Standard-Journal mit. Die Mod prüft ihre Bedingungen
  deshalb selbst, und so funktioniert die Kategorie mit **jedem** Charakter.

### 2.6 Journal: „Journal - Aufgabe“ (23 bis 63 Checks, je nach Charakter)

Jeder Charakter hat ein eigenes Journal. Das Spiel verfolgt nur die Aufgaben im Journal
des gewählten Charakters:

| Journal | Aufgaben | Beispiele |
| --- | --- | --- |
| Standard (Survivalist, Normal Guy) | 35 | 19 Erkundungen, 7 Skills auf Maximum, Floß, Komfort |
| Farmer | 63 | Pflanzen, Großvater bekochen, Hof wieder aufbauen |
| Hunter | 40 | 9 Tiere jagen, Ausrüstung, Schamane/Krieger |
| Mermaid | 23 | Nest, Schwangerschaft, Seehunde |
| Tourist | 32 | Fotos, Souvenirs, Party |

Vorschlag:

- Eine YAML-Option `journal: off / on`. Dazu kommt die neue Option `character`, die den
  Charakter festlegt.
- Generiert wird nur dieses eine Journal.
- Die Mod warnt, wenn ein anderer Charakter gewählt wird.
- Aufgaben, die schon eine andere Kategorie abdeckt (Erkunden, Jagd), werden nicht doppelt
  gezählt.
- Ziel-Aufgaben (Zivilisation erreichen, 120 Tage) werden ausgeschlossen.
- „Skill auf Maximum“ (Wert 150) wird ein **Challenge-Check**.

### 2.7 Weitere, kleinere Kategorien (alle optional)

| Kategorie | Checks | Beispiele | Anmerkung |
| --- | --- | --- | --- |
| Erfolge (`Ach_*`) | ca. 25 von 54 | Kupferwerkzeug, Ei ausbrüten, Ziege bekommt Junges, Makaken-Freund | ohne Tod, Sieg, Charakter und Maximal-Skills |
| Tiere und Begleiter | ca. 5 | Makaken-Freund (frei), Ziege im Gehege, Rebhuhn-Küken | überschneidet sich mit den Erfolgen |
| Überlebenstage | 5 | Tag 7, 14, 30, 60, 100 | spätere Tage werden Challenge-Checks |
| Skill-Meilensteine | 44 | jeder Skill auf 50 und 100 | Achtung: Skill-Items lösen sie mit aus, darum eher nicht |

## 3. Was in dein Spiel geschickt werden kann

Heute gibt es Blueprints, Skills, Perks und Vorräte. Mit z. B. 200 neuen Checks braucht
das Spiel 200 neue Items. Nur mit Vorräten wird das schnell langweilig. Deshalb:

### 3.1 Neue nützliche Items (an, sobald die neuen Checks an sind)

1. **Themenpakete statt allgemeiner Vorräte.** Der Mechanismus existiert schon
   (`FILLERS`). Die genauen Karten kommen aus den Daten.
   - Fischerpaket (gegarter Fisch, Köder)
   - Jägerpaket (gegartes Fleisch, Fett)
   - Kochpaket (Salz, Gewürze, Kokosnuss)
   - Heilpaket (Verbände, Heilkräuter)
   - Baupaket (Holz, Steine, Fasern, Lehm)
2. **Erholung.** Das ist eine kurze Wirkung auf einen Wert, über denselben Weg wie die
   Skills (`ApplyStatModifier`).
   - „Gute Mahlzeit“: Sättigung +
   - „Frisches Wasser“: Hydration +
   - „Ruhiger Moment“: Stress −
   - „Mut“: Courage +

   Das sind keine dauerhaften Boni, also kein „Spiel wird leichter“.
3. **Werkzeugkiste.** Ein neues Exemplar eines Werkzeugs, dessen Blueprint du schon hast.
   Das ersetzt abgenutzte Werkzeuge.
4. **Begleiter: Makaken-Freund.** Die Karte gibt es im Spiel, und sie ist frei
   erreichbar. Sie wäre ein seltenes, nützliches Item.

### 3.2 Optionale Fortschritts-Items (aus, ändern das Spiel stärker)

5. **Kochbuch: „Rezept: X“.**
   - Was es tut: Die 17 Gerichte lassen sich erst kochen, wenn ihr Rezept-Item da ist.
     So wird Kochen zu echtem Fortschritt.
   - Was dabei entsteht: 17 neue Fortschritts-Items.
   - Technik: Das Kochrezept im Topf wird gesperrt (eigener Patch).
   - Logik: „Cook - X“ verlangt dann zusätzlich das Rezept.

### 3.3 Fallen (aus, Anteil per Option `trap_percentage`)

Archipelago-üblich: Einige Füll-Items werden durch Fallen ersetzt. Die Fallen nutzen nur
Ereignisse, die es im Spiel schon gibt:

| Falle | Was passiert | Quelle in den Daten |
| --- | --- | --- |
| Affenüberfall | ein Makak greift an | `Combat_EventMacaque_1_Raid` |
| Wildschwein | ein Wildschwein greift an | `Combat_EventBoar_1_Raid` |
| Bienenstiche | Stiche | Wert `BeeStings` |
| Magenverstimmung | Durchfall | Wert `Diarrhoea` |
| Müdigkeit oder Hunger | Wert sinkt | Werte für Sättigung und Ausdauer |

Durch den Safe Mode kann eine Falle dich nie endgültig töten. Ob ein Kampf-Ereignis sich
per Item sauber auslösen lässt, muss ein Test im Spiel zeigen.

### 3.4 Bewusst nicht

- Gebietsschlüssel (Regel: keine Gebietssperren).
- DeathLink.
- Determination als Item: Es würde die Endings abkürzen.
- Dauerhafte Werte-Boni über die Perks des Spiels hinaus.

## 4. Mengen im Überblick

| Kategorie | Checks | davon frei |
| --- | --- | --- |
| Heute (Forschung, Bauen, Besuchen) | 451 | – |
| Fischen | 13 | 12 |
| Jagd | 10 | 9 |
| Kochen (Gerichte / alles) | 17 / 74 | 0 / 32 |
| Anbau | 25 | 0 |
| Erkunden | 19 | die meisten |
| Journal (Standard) | ca. 10 nach Abzug der Doppelten | – |
| Erfolge, Tage, Tiere | ca. 35 | – |

Mit allem sind das bis zu rund **640 Checks**. Wer kurze Runden will, lässt die
Kategorien aus.

## 5. Reihenfolge der Umsetzung

1. **Phase A: ein Mechanismus, vier Kategorien.**
   - Der `obtain`-Check in Registry, APWorld und Plugin.
   - Darauf aufbauend Fischen, Jagd, Kochen und Anbau, jeweils mit eigener YAML-Option.
   - Dazu die Themenpakete und die Erholungs-Items.
   - Tests: Registry, Selbsttest im Spiel, echte Seeds.
2. **Phase B: Erkunden und Journal.**
   - Der `objective`-Check und die Option `character`.
3. **Phase C: Extras.**
   - Erfolge, Tage, Fallen, Kochbuch.
   - Skill-Meilensteine nur, wenn gewünscht.

Jede Phase bekommt eine eigene Version (`world_version` und `Plugin.Version` zusammen).

## 6. Offene Entscheidungen

1. Welche Kategorien sollen gebaut werden? Vorschlag: Phase A komplett.
2. Kochen als `meals` (17) oder `all` (74)? Vorschlag: beide anbieten, Standard `meals`.
3. Kochbuch-Items (Kochen wird gesperrt): ja oder nein? Vorschlag: als Option, aus.
4. Fallen: ja oder nein? Vorschlag: als Option, Anteil 0 %.
5. Journal: Charakter per YAML festlegen? Vorschlag: ja, Standard „Survivalist“.
