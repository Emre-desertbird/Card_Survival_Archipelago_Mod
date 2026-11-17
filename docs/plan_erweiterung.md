# Plan: neue Checks, Logbuch, neue Perks, Items und Balance

Status: **Alle Phasen A bis D sind gebaut** (Mod-Version 0.7.2, je Phase im echten Spiel
getestet). Das Logbuch steht auch im Journal-Buch des Spiels.

Abweichungen beim Bau, jeweils aus den Daten begründet:

- Logbuch: **44 Aufgaben** statt 40. Der Säuresee ist ein eigenes Erkundungsziel im Spiel,
  und „Bote“ hat drei Stufen (50/100/200). Summe 5.300 Determination, mindestens 4.000 auch
  ohne die optionalen Check-Arten.
- Logbuch „Vorrat“ (trocknen) heißt jetzt „Smokehouse“ (räuchern): Getrocknetes Fleisch ist
  ein Fundstück und hätte die Aufgabe sonst von selbst erledigt.
- Pantry Keeper (Vorratshalter) wirkt auf **alles**, was verdirbt, nicht nur im Lager. Das
  Spiel unterscheidet „eigenes Lager“ nicht sauber.
- Die Charaktersperre lenkt den Start auf Normal Guy um. Er steht nicht in der
  Charakterliste des Spiels, er ist nur die Vorlage für eigene Charaktere.
- Extras: **11 Spielerfolge** statt etwa 25. Die übrigen sind Doppelte zu anderen Checks,
  verlangen Schaden (gebissen werden, Bienenschwarm), hängen an Charakter, Tod, Sieg oder
  Maximal-Skills, oder sind für Normal Guy unerreichbar („Defeat the Hunter“, automatisch
  weggelassen). Dazu 3 Tierhaltungs- und 3 Überlebenstage-Checks (7/14/30; 60 und 100 Tage später gestrichen, weil sie nur Warten verlangen): zusammen 17.
- Ausbau-Set: geht nur bei genau einer Hütte dieser Art und noch keiner der nächsten Art
  (sonst wären die gespeicherten Innenräume nicht eindeutig). Die Mod speichert dafür kurz
  und lädt neu.
- Die Stauraum-Erweiterung gibt 25 % des Platzes der *aktuellen* Hüttenart; nach einem Ausbau
  wächst sie also mit.
- Item-Namen sind englisch wie alle Archipelago-Namen: Pack Mule (Lastenträger), Nimble
  Hands (Geschickte Hände), Pathfinder (Pfadfinder), Careful Hands (Sorgsam), Inquisitive
  Mind (Forschergeist), Pantry Keeper (Vorratshalter).

- Zahlen und Kartennamen stammen aus dem Daten-Dump (`.local/dump`), dem dekompilierten
  Code und `tools/card_graph.py`.
- „frei“ heißt: ohne ein Archipelago-Item erreichbar.
- 1 Spieltag hat 96 Ticks à 15 Minuten.
- Projektregel: **Neue Spielmechaniken sind optional und standardmäßig aus.** Das gilt
  hier für neue Perks, Ausbau-Set und Fallen.

Was sich gegenüber Version 2 geändert hat:

- **Keine Progressive-Linien** (Unterkunft, Bett, Lager): Sie würden eher bremsen als
  helfen.
- **Das Ausbau-Set wirkt nur in einer Hütte.**
- **Normal Guy ist der Archipelago-Charakter.** Er bekommt ein eigenes Logbuch mit neuen
  Aufgaben. Andere Charaktere werden im Spiel gesperrt.
- **Neue Perks**, die es im Spiel nicht gibt, darunter progressive Tragkraft.

---

## 1. Normal Guy mit Archipelago-Logbuch

### 1.1 Warum Normal Guy

Normal Guy ist laut Daten der einzige ganz neutrale Charakter:

- keine Perks, kein Startkit
- Start am Strand
- normale Kleidung (T-Shirt, Socken, Unterwäsche, Shorts)

Das passt zu „mit nichts starten“, und die Logik kann sich genau darauf verlassen. Einen
neuen Charakter muss die Mod dafür nicht erfinden.

### 1.2 Andere Charaktere sperren

Das geht. Das Spiel baut die Charakterliste im Hauptmenü aus einer Liste
(`MainMenu`, `Gamemode.PlayableCharacters`). Eigene Charaktere haben einen eigenen Reiter.
Ist die Mod mit einem Server verbunden, gilt:

1. Im Hauptmenü sind alle Charaktere außer Normal Guy ausgegraut, auch die eigenen
   („Custom“).
2. Zusätzlich gibt es eine Sicherheitssperre beim Start eines neuen Spiels: Ist ein
   anderer Charakter gewählt, startet das Spiel nicht, und eine Meldung erklärt warum.
3. Ohne Verbindung ist das Spiel völlig normal, und alle Charaktere sind frei.

YAML-Option `character_lock`: an (Standard). Wer sie ausschaltet, darf jeden Charakter
spielen. Dann fallen die Logbuch-Checks weg, und die Logik kann bei Charakteren mit
anderem Startgebiet ungenau sein.

### 1.3 Wie das Logbuch funktioniert

- In einem Archipelago-Spielstand ersetzt das Logbuch das normale Journal von Normal Guy.
  Ersetzt wird es, damit die **Determination** nicht doppelt kommt.
- Jede Logbuch-Aufgabe ist ein **Check** („Logbuch - …“). Sie gibt außerdem Determination,
  genau wie Journal-Aufgaben im Original.
- **Das Home-Ending braucht 4.000 Determination.** Das Logbuch gibt zusammen etwa 5.000.
  Dazu kommt, was das Spiel ohnehin gibt: Hütten 100 bis 250, das Floß laufend.
- Aufgaben verlangen nie, etwas zu **erhalten**. Erlaubt sind eigene Taten und das
  **Verschicken** eigener Checks.
- Anzeige:
  1. Zuerst als Seite im F8-Fenster, das ist sicher.
  2. Danach versuche ich, es in das echte Journal-Buch einzuhängen
     (`ContentDisplayer`, Seiten zur Laufzeit).

### 1.4 Die Aufgaben (Entwurf, alle für Normal Guy machbar)

Alle genannten Karten gibt es in den Daten. Die genauen Bedingungen und die Logik erzeugt
`build_registry.py` aus den Daten.

**Kapitel 1: Gestrandet** (je 50 Determination)

| Aufgabe | Bedingung |
| --- | --- |
| Erste Stärkung | eine Kokosnuss trinken und essen |
| Funken | ein Lagerfeuer (`Campfire`) |
| Scharfer Verstand | einen geschärften Stein herstellen |
| Wasserstelle | eine Pfütze finden (`Puddle`) |
| Dach über dem Kopf | einen Shelter bauen |

**Kapitel 2: Überleben** (je 100)

| Aufgabe | Bedingung |
| --- | --- |
| Sauberes Wasser | Wasser abkochen (`LQ_WaterBoiling`) |
| Vorrat | Fisch oder Fleisch trocknen (`FishDried` / `*Dried`) |
| Erste Hilfe | eine Wunde verbinden |
| Kühler Kopf | einen Clay Pot Cooler bauen |
| Ein echtes Essen | ein Topfgericht kochen |

**Kapitel 3: Die Insel** (je 100, 19 Aufgaben)

- Jedes Gebiet vollständig erkunden, also der Erkundungsbalken ist voll.
- Das ersetzt die eigene Kategorie „Erkunden“ aus Version 2.

**Kapitel 4: Handwerk** (je 250)

| Aufgabe | Bedingung |
| --- | --- |
| Töpfer | ein Brennofen (`Kiln`) |
| Weber | ein Webstuhl (`Loom`) |
| Seilmacher | ein Seil (`Rope`) |
| Bronzezeit | ein Kupferwerkzeug (Axt oder Messer) |
| Fischer | eine Angelrute (`FishingRod`) |

**Kapitel 5: Multiworld-Tagebuch** (je 100; nur eigene Checks)

| Aufgabe | Bedingung |
| --- | --- |
| Petri Heil | 10 Fischen-Checks verschickt |
| Waidmann | 5 Jagd-Checks verschickt |
| Küchenchef | 8 Kochen-Checks verschickt |
| Gelehrter | 25 Forschungs-Checks verschickt |
| Baumeister | 25 Bau-Checks verschickt |
| Weltenbummler | 15 Gebiete betreten |
| Bote | 50 / 100 / 200 Checks insgesamt verschickt |

Für die Logik gilt: „N Checks einer Art“ heißt, dass N dieser Checks erreichbar sein
müssen. Das ist in Archipelago gut abbildbar.

**Kapitel 6: Aufbruch** (500)

- Das Floß bauen.

**Summe:**

| Kapitel | Rechnung | Determination |
| --- | --- | --- |
| 1 | 5 × 50 | 250 |
| 2 | 5 × 100 | 500 |
| 3 | 19 × 100 | 1.900 |
| 4 | 5 × 250 | 1.250 |
| 5 | 9 × 100 | 900 |
| 6 | 1 × 500 | 500 |
| **Gesamt** | | **5.300** |

Das sind **40 Logbuch-Checks**.

---

## 2. Die Checks im Überblick

| Kategorie | Checks | Was zählt | Standard |
| --- | --- | --- | --- |
| Forschung, Bauen, Besuchen | 451 | wie heute | an |
| Fischen | 13 | erster Fang jeder Art, 12 davon frei | aus |
| Jagd | 10 | erster Kadaver je Tierart, 9 davon frei | aus |
| Kochen | 17 | jedes Topf- und Kokosschalen-Gericht einmal | aus |
| Anbau | 5 | erste Pflanze, erste Ernte, erstes Pilzbeet, 3 und 6 verschiedene Pflanzen | aus |
| Logbuch | 40 | Abschnitt 1.4; braucht `character_lock` | aus |
| Extras | ca. 35 | Erfolge (ca. 25), Überlebenstage 7/14/30/60/100, Tiere | aus |

Doppelte werden nur einmal gezählt. Mit allem sind es etwa **570 Checks**.

---

## 3. Neue Perks (gibt es so nicht im Spiel)

Im Spiel gibt es schon Skill-Perks (Fisher, Cook …), Stoffwechsel, Immunsystem und
Ähnliches. Diese kommen schon heute als Items. Die neuen Perks decken **andere** Dinge ab.
Sie sind **Nützlich**, die Logik verlässt sich nie auf sie, und sie sind über die Option
`new_perks` zuschaltbar.

| Perk | Stufen | Wirkung | Technik | Machbarkeit |
| --- | --- | --- | --- | --- |
| **Lastenträger** (progressiv) | 3 | Getragenes Gewicht zählt nur 90 / 80 / 70 %. Die Belastungsgrenze (heute ab 2.000) liegt dann bei 2.222 / 2.500 / 2.857. | Das Spiel rechnet das Gewicht in `GameManager.CalculateCarryWeight` in einen Wert um; die Mod setzt dort den Faktor an. | gut, klar im Code |
| **Geschickte Hände** | 1 | Bauschritte von Blueprints dauern 33 % kürzer, gerundet, nie unter 15 Min. | Patch an der Bauzeit (`BuildingDaytimeCost`) | gut |
| **Pfadfinder** | 1 | Reisen 33 % schneller, gerundet, nie unter 15 Min. | Patch an der Reisezeit (`TotalDaytimeCost` bei `TravelAction`) | gut |
| **Sorgsam** (progressiv) | 2 | Werkzeuge nutzen sich 15 / 30 % langsamer ab | `ActionModifier.GivenDurabilities` oder Patch an der Abnutzung | prüfen |
| **Forschergeist** (progressiv) | 2 | Forschung 15 / 30 % schneller, also mehr Checks pro Tag | Patch am Forschungsfortschritt | prüfen |
| **Vorratshalter** | 1 | Essen im eigenen Lager verdirbt 15 % langsamer | Patch an der Verderb-Rate | prüfen |

**Warum Prozent statt fester Minuten:**

- Das Spiel rechnet Dauer in ganzen Ticks à 15 Min. und begrenzt sie unten auf 0.
- 34 von 42 Reisen dauern genau 1 Tick. Mit „−1 Tick“ wären sie sofort.
- Deshalb gilt für beide Perks: Dauer × 0,67, normal gerundet, mindestens 1 Tick.
  - 1 Tick bleibt 1 Tick.
  - 2 Ticks werden 1 Tick.
  - 3 Ticks werden 2 Ticks.
  - 6 Ticks werden 4 Ticks.
- Kurze Wege bleiben also unverändert, lange werden spürbar kürzer.
- Gebaut wird als eigener kleiner Patch statt mit `ActionModifier`. Denn `ActionModifier`
  kennt nur feste Tick-Abzüge, und das Merkmal `CraftAction` tragen nur 5 Aktionen.

Zusammen sind das **10 Perk-Items**. Weitere Stufen sind bewusst klein gehalten, damit das
Spiel ein Überlebensspiel bleibt. „Prüfen“ heißt: Die Stelle im Code muss ich vor dem Bau
noch genau ansehen. Klappt eine davon nicht sauber, fällt sie weg.

---

## 4. Upgrades

- **Ausbau-Set** (2 im Pool, optional):
  - Es wirkt **nur, wenn du in einer Hütte stehst**: Shed → Mud Hut → Stone Hut.
  - Die Hütte steigt ohne Material eine Stufe auf, und der Inhalt bleibt.
  - In einer Stone Hut oder draußen ist der Knopf ausgegraut, und das Item bleibt
    erhalten.
  - Technik: Die Eingangskarte wird getauscht, und der gespeicherte Innenraum zieht unter
    die neue ID um. Mittlerer Aufwand, braucht Tests mit Checkpoints.
- **Stauraum-Erweiterung** (3 im Pool, optional): +25 % Platz für den Innenraum, in dem
  du stehst. Auch das wirkt nur in einer Hütte oder im Keller.
- **Keine Progressive-Linien** und **kein Kühlschrank-Upgrade**.

---

## 5. Fallen (optional, harmlos, gedeckelt)

| Falle | Wirkung |
| --- | --- |
| Schürfwunde | `W_Abrasion`, **das Schlimmste** |
| Blauer Fleck | `W_Bruise` |
| Kratzer | `W_LizardScratch` |
| Regenschauer | Wetter wird leichter Regen |
| Mückenschwarm | Mückenstiche + |
| Dreck | Schmutz + |
| Schreck | Stress + |

Nicht dabei sind Kämpfe, Brüche, Krankheiten, Gift und Item-Verlust.

Grenzen:

- `trap_percentage` (Standard 0 = aus) legt fest, wie viele Füll-Items zu Fallen werden.
- `trap_limit` (Standard 10) gilt auch bei 100 %.
- Höchstens eine Falle pro Spieltag.
- Keine Fallen auf See.

---

## 6. Was du als Spieler bekommst, und warum die Balance hält

### 6.1 Der Item-Pool (alles an, etwa 570 Checks)

| Klasse | Was | Anzahl |
| --- | --- | --- |
| Fortschritt | Blueprints | 194 |
| Nützlich | Skill-Stufen (+25) | 66 |
| Nützlich | Perks aus dem Spiel | 11 |
| Nützlich | neue Perks (Abschnitt 3) | 10 |
| Nützlich | Ausbau-Set 2, Stauraum 3 | 5 |
| Füller | Fundstücke (6.2) | ca. 285 |
| Falle | aus Füllern umgewandelt | höchstens 10 |

### 6.2 Fundstücke

„Ein mysteriöses Wesen hat etwas zurückgelassen“: Es liegt **immer** auf dem Boden des
Gebiets, in dem du gerade bist. Stehst du in einer Hütte, liegt es im Innenraum. Gewählt
sind nur Dinge, die **im Original schon lange halten**.

**Limit und Zwischenspeicher:**

1. Höchstens **10 Fundstücke pro Spielstunde** landen direkt auf dem Boden.
2. Alles darüber kommt in den **Zwischenspeicher**. Er liegt in der Slot-Datei, geht also
   auch bei Tod oder Checkpoint nicht verloren.
3. Der Zwischenspeicher ist ein eigener Reiter im F8-Fenster. Dort siehst du jedes
   wartende Fundstück mit Inhalt („3 Stöcke“, „Ingwertee in Kokosschale“) und von wem es
   kam.
4. Du holst einzelne Fundstücke oder „alle“ (wieder höchstens 10 auf einmal) ab. Sie
   landen im Gebiet, in dem du gerade stehst.
5. Wartet etwas, zeigt die Statuszeile das an, z. B. „Zwischenspeicher: 14“.

| Paket | Anteil | Inhalt (eines davon) | Hält im Original |
| --- | --- | --- | --- |
| Material | 35 % | 3 Stöcke, 2 Steine, 3 Fasern, 2 Palmwedel oder 1 Lehm | verdirbt nicht |
| Kochzutaten | 20 % | Salz, getrocknete Chilis, getrockneter Ingwer, Kokosnuss, Honig, Fett | lange |
| Proviant | 20 % | getrocknetes Fleisch, Pökelfleisch, 2 gekochte Eier, Ziegenkäse | 11 bis 21 Tage |
| Getränke | 15 % | Wasser in einer Kokosschale, Ingwer- oder Jasmintee | Wasser nie, Tee 5 Tage |
| Genuss | 10 % | kandierter Ingwer, Schokolade, Palmwein, Met | 10 bis 30 Tage |

### 6.3 Leitplanken

1. **Ein Fundstück spart etwa 1 bis 2 Stunden Sammeln.** Über das ganze Spiel sind das
   etwa 57 Mahlzeiten und 43 Getränke. Bei 60 bis 100 Tagen ist das etwa jeden zweiten Tag
   eine Mahlzeit: eine Hilfe, kein Ersatz.
2. **Nichts Wertvolles als Füller.** Kein Kupfer, kein Obsidian, kein Metall, kein Seil,
   kein Stoff, keine Werkzeuge. Flüssigkeiten kommen nur in Kokosschale oder Tonschale.
3. **Lieferbremse:**
   - Das Limit und der Zwischenspeicher aus 6.2 schützen zum Beispiel bei einem
     `!release`, wenn hunderte Items auf einmal kommen.
   - Blueprints, Skills und Perks kommen immer sofort.
4. **Regler:** `finds_amount: less / normal / more`.
   - `less`: halbe Mengen; der Rest wird zu wertlosen Andenken (hübsche Muschel).
   - `more`: anderthalbfache Mengen.
5. **Dauerhafte Boni bleiben klein:**
   - Skills: höchstens 3 × 25 je Skill, wie die Skill-Perks des Spiels.
   - Neue Perks: in kleinen Stufen (Abschnitt 3).
6. **Die Logik verlässt sich nie auf Füller, Perks, Skills oder Upgrades.** Jeder Seed ist
   auch ohne Glück schaffbar.

### 6.4 So fühlt es sich an (Solo, alles an)

| Phase | Was du tust | Was du ungefähr bekommst |
| --- | --- | --- |
| Tag 1 bis 10 | Strand, Fischen, Logbuch Kapitel 1, erste Forschung | 10 bis 15 Blueprints, 2 bis 3 Skill-Stufen, Lastenträger 1, Material und Wasser |
| Tag 10 bis 40 | Hütte, Topf, Jagd, Gerichte, Erkunden | Blueprints für Hütte und Topf, Geschickte Hände oder Pfadfinder, Kochzutaten, ein Ausbau-Set |
| Tag 40+ | Kupfer, Floß, Logbuch beenden | späte Blueprints, Lastenträger 2 bis 3, Stauraum, Rest der Skills |

---

## 7. Reihenfolge der Umsetzung

1. **Phase A: neue Checks.**
   - Ein gemeinsamer Mechanismus „zum ersten Mal X“.
   - Darauf aufbauend Fischen, Jagd, Kochen und Anbau.
   - Fundstücke mit Lieferbremse und Briefkasten (ersetzen die heutigen Vorräte).
2. **Phase B: Logbuch.**
   - Charaktersperre und Logbuch (40 Checks).
   - Zuerst im F8-Fenster, danach im Journal-Buch.
3. **Phase C: neue Perks.**
   - Zuerst Lastenträger, Geschickte Hände, Pfadfinder.
   - Dann die „prüfen“-Perks.
4. **Phase D: Ausbau-Set, Stauraum, Extras, Fallen.**

Jede Phase bekommt eine eigene Version, Tests im echten Spiel und eine aktualisierte
spielbare Kopie.

---

## 8. Offene Entscheidungen

1. **Neue Perks und Ausbau-Set:** Nach Projektregel sind sie standardmäßig aus. Sollen
   sie für dich standardmäßig **an** sein (Regel anpassen), oder bleibt es bei „aus,
   per YAML einschaltbar“?
2. **Logbuch-Aufgaben:** Passt der Entwurf in 1.4? Fehlt etwas, oder ist etwas zu schwer?
3. **`character_lock`:** standardmäßig an. Einverstanden?
4. **Fundstücke:** Passen Anteile und Inhalt in 6.2?
