# Plan: neue Checks, Archipelago-Charakter, Items und Balance

Status: **Vorschlag, noch nicht gebaut** (Version 2 des Plans).

- Zahlen und Kartennamen stammen aus dem Daten-Dump (`.local/dump`), dem dekompilierten
  Code und `tools/card_graph.py`.
- „frei“ heißt: ohne ein Archipelago-Item erreichbar.
- 1 Spieltag hat 96 Ticks à 15 Minuten.

---

## 1. Antworten auf die Fragen

### 1.1 „Progressive Hut“ und „Storage Upgrade“: geht das?

**Ja, in zwei Formen.** Was die Daten zeigen:

| Linie | Stufen im Spiel | Platz |
| --- | --- | --- |
| Unterkunft | Shelter → Shed → Mud Hut → Stone Hut | Innenraum 15.000 → 60.000 → 70.000 |
| Tragen | Satchel (350) → Woven Basket (1.000) | – |
| Lager | Chest (4.000) → Cellar (Innenraum 30.000, kühl) | – |
| Kühlen | Clay Pot Cooler | halbiert den Verderb, **nur eine Stufe** |

Shed, Mud Hut, Stone Hut und Cellar sind **eigene kleine Gebiete** mit Eingangskarte. Der
Inhalt gehört zu diesem Innenraum und wird mit dessen ID gespeichert
(`CardData.EnvironmentDictionaryKey`). Ein Ausbau an Ort und Stelle muss deshalb den
Innenraum umziehen: Eingangskarte tauschen und die Speicherdaten unter die neue ID setzen.
Das ist machbar, aber mittlerer Aufwand und braucht gründliche Tests mit Checkpoints.

Vorschlag:

1. **Progressive Unterkunft**: die übliche Archipelago-Lösung, standardmäßig an. Die drei
   Blueprint-Items Shed, Mud Hut und Stone Hut werden zu dreimal „Progressive Unterkunft“.
   Du bekommst sie immer in der richtigen Reihenfolge, bauen musst du sie weiter selbst.
   Genauso geht es mit **Progressives Bett** (Rustic Bed → Wooden Bed) und
   **Progressives Lager** (Woven Basket → Chest → Cellar).
2. **Ausbau-Set** (Verbrauchs-Item, deine Idee, optional):
   - Einlösen: über das F8-Fenster, während du in oder vor deiner Hütte stehst.
   - Wirkung: Die Hütte steigt eine Stufe auf, ohne Material, und der Inhalt bleibt.
   - Menge im Pool: 2 (Shed → Mud Hut → Stone Hut).
   - Die Logik verlässt sich nie darauf.
3. **Stauraum-Erweiterung** (Verbrauchs-Item, optional):
   - Wirkung: +25 % Platz für den Innenraum, in dem du stehst (Hütte oder Keller).
   - Menge im Pool: 3.
   - Technik: Die Mod merkt sich den Bonus und setzt ihn nach jedem Laden neu.
4. **Kein Kühlschrank-Upgrade.** Das Spiel kennt nur eine Kühlstufe. Eine neue Stufe
   wäre eine erfundene Spielmechanik.

### 1.2 Eigener Archipelago-Charakter statt YAML-Eintrag

**Ja, das ist die bessere Lösung.**

- Das Spiel hat mit **Normal Guy** schon einen neutralen Charakter: keine Perks, kein Kit,
  normale Kleidung, Start am Strand.
- Der neue Charakter „**Archipelago-Reisender**“ ist eine Kopie davon, mit eigenem Namen und
  eigenem **Logbuch**.
- Die Logik kann sich genau auf ihn verlassen.

Technik:

- Die Mod erzeugt beim Start eine Kopie von Normal Guy und hängt sie in die
  Charakterliste.
- Im Spielstand steht die ID von Normal Guy, damit die Datei auch ohne Mod lesbar bleibt.
  Dass es der Archipelago-Charakter ist, steht in der Slot-Datei der Mod.

Wichtig für die Balance: Im Standard-Journal sammelt man **Determination**:

| Stufe | Determination je Aufgabe |
| --- | --- |
| frühe Aufgaben | 50, 100, 250, 500 |
| ein Gebiet ganz erkundet | 100 |
| ein Skill auf Maximum | 250 |
| Hütten bauen | 100 bis 250 |

Das **Home-Ending braucht 4.000**. Das Logbuch muss also ähnlich viel geben, sonst fehlt
ein Ending.

Das **Logbuch** (eigenes Journal) hat etwa 30 Aufgaben in Kapiteln:

| Kapitel | Beispiele | Determination |
| --- | --- | --- |
| Gestrandet | Kokosnuss trinken, Feuer machen, Werkzeug schärfen | je 50 |
| Sich einrichten | Unterkunft, Wasser abkochen, erste Mahlzeit | je 100 |
| Die Insel | jedes Gebiet ganz erkunden (19) | je 100 |
| Handwerk | Ofen, Webstuhl, Kupferwerkzeug, Floß | je 250 bis 500 |
| Multiworld | 1. Item von einem anderen Spieler, 25/100 eigene Checks, einem anderen Spieler helfen | je 100 |
| Abschied | entschlossen sein (wie im Original) | – |

Die Summe liegt wie im Original über 4.000. Aufgaben, die von anderen Spielern abhängen,
sind **keine Logik-Bedingung**. Sie geben nur etwas zusätzlich.

Umsetzung in zwei Schritten:

1. Das Logbuch erscheint zuerst als eigene Seite im F8-Fenster. Das ist sicher.
2. Danach probiere ich, es in das echte Journal-Buch des Spiels einzuhängen.

YAML: `character: archipelago` ist Standard. Mit `any` darf jeder Charakter gespielt
werden; dann entfallen die Logbuch-Checks.

### 1.3 Anbau: alles anbauen ist zu viel

Einverstanden. Statt 25 Checks (je Pflanze einer) gibt es **5 Checks**:

- erste Pflanze gesetzt
- erste Ernte
- erstes Pilzbeet
- drei verschiedene Pflanzen gesetzt
- sechs verschiedene Pflanzen gesetzt

### 1.4 Kochbuch: gestrichen

Im Original kannst du jedes Gericht kochen, sobald du Topf und Zutaten hast. Man lernt
dafür nichts. Das Kochbuch hätte dafür eine neue Sperre erfunden. Gestrichen, wie
gewünscht. Kochen bleibt nur bei den Checks („Cook - X“).

### 1.5 Fallen: harmlos und gedeckelt

| Falle | Wirkung | Quelle |
| --- | --- | --- |
| Schürfwunde | `W_Abrasion` | **das Schlimmste**, eine leichte Wunde |
| Blauer Fleck | `W_Bruise` | – |
| Kratzer | `W_LizardScratch` | – |
| Regenschauer | Wetter wechselt zu leichtem Regen | `TropicalIsland_LightRain` |
| Mückenschwarm | Mückenstiche + | Wert `BugBites` |
| Dreck | Schmutz + | Wert `Filth` |
| Schreck | Stress + | Wert `Stress` |

Nicht dabei sind Kämpfe, Brüche, Krankheiten, Gift und Item-Verlust.

Grenzen:

- `trap_percentage` 0 bis 100 legt fest, wie viele Füll-Items zu Fallen werden.
  Standard: **0 = aus**.
- `trap_limit` deckelt die Zahl, Standard 10. Auch bei 100 % gibt es nie mehr als so
  viele Fallen.
- Höchstens **eine Falle pro Spieltag**. Weitere warten bis zum nächsten Tag.
- Auf See und auf dem Floß gibt es keine Fallen.

---

## 2. Die Checks im Überblick

| Kategorie | Checks | Was zählt | Standard |
| --- | --- | --- | --- |
| Forschung, Bauen, Besuchen | 451 | wie heute | an |
| Fischen | 13 | erster Fang jeder Art, 12 davon frei | aus |
| Jagd | 10 | erster Kadaver je Tierart, 9 davon frei | aus |
| Kochen | 17 | jedes Topf- und Kokosschalen-Gericht einmal | aus |
| Anbau | 5 | siehe 1.3 | aus |
| Logbuch | ca. 30 | Aufgaben des Archipelago-Charakters, inklusive 19 × Gebiet ganz erkunden | aus |
| Extras | ca. 35 | siehe unten | aus |

**Extras:**

- Spielerfolge (`Ach_*`): etwa 25 von 54, z. B. Kupferwerkzeug, Ei ausbrüten, Ziege bekommt
  Junges, Makaken-Freund, Bienenschwarm, Dynamit. Ohne Tod, Sieg, Charakterwahl und
  „Skill auf Maximum“.
- Überlebenstage: 7, 14, 30, 60, 100. Ab Tag 60 zählen sie als Challenge-Check.
- Tiere: Ziege im Gehege, Rebhuhn-Küken, Ferkel und weitere.

Doppelte werden nur einmal gezählt. „Jage einen Wildschwein-Kadaver“ ist zum Beispiel ein
Jagd-Check und kein zweiter Erfolg-Check.

Mit allem sind es etwa **575 Checks**. Jede Kategorie kann einzeln ausgeschaltet werden.

---

## 3. Was du als Spieler bekommst, und warum es die Balance nicht zerstört

### 3.1 Die Item-Klassen

| Klasse (Archipelago) | Was | Wie viele | Wirkung |
| --- | --- | --- | --- |
| Fortschritt | Blueprints, als Progressive-Linien wo sinnvoll | 194 | öffnen Bauen, also das eigentliche Spiel |
| Nützlich | Skill-Stufen (+25) | 66 | wie heute |
| Nützlich | Perks aus dem Spiel | 11 | wie heute |
| Nützlich | Ausbau-Set, Stauraum-Erweiterung | 2 + 3 | optional |
| Füller | **Fundstücke**, siehe 3.2 | der Rest, etwa 290 | Zeitersparnis |
| Falle | siehe 1.5 | höchstens 10 | ärgerlich, nie gefährlich |

### 3.2 Fundstücke: das neue Füll-System

Statt einer Karte in der Hand legt „ein mysteriöses Wesen“ etwas **auf den Boden des
Gebiets**, in dem du gerade bist. Eine Meldung sagt, was es war. Gewählt sind nur Dinge,
die **im Original schon lange halten**. Die Mod muss also keine Haltbarkeit verändern.

| Paket | Inhalt (je Fundstück eines davon) | Hält im Original |
| --- | --- | --- |
| Proviant | getrocknetes Wildschwein-, Ziegen- oder Makakenfleisch; Pökelfleisch; 2 gekochte Eier; Ziegenkäse | 11 bis 21 Tage |
| Süßes | kandierter Ingwer, Schokolade | 30 Tage |
| Getränk | Wasser in einer Kokosschale; Ingwertee, Jasmintee | Wasser verdirbt nie, Tee 5 Tage |
| Alkohol | Palmwein, Met | 10 Tage |
| Kochzutaten | Salz, getrocknete Chilis, getrockneter Ingwer, Kokosnuss, Honig, Fett | lange |
| Material | 3 Stöcke, 2 Steine, 3 Fasern, 2 Palmwedel oder 1 Lehm | verdirbt nicht |

Bei Flüssigkeiten wird der **Behälter mitgegeben**. Deshalb gibt es nur billige Behälter:
Kokosschale oder Tonschale, nie Feldflasche, Kanister, Kupferflasche oder Plastikflasche.

### 3.3 Leitplanken gegen kaputte Ökonomie

1. **Ein Fundstück ersetzt etwa 1 bis 2 Stunden Sammeln, nie einen Tag.** Ein Fleischstück
   ist etwa eine Mahlzeit, eine Kokosschale Wasser etwa ein halber Tag Trinken.
2. **Aufteilung im Pool:**

   | Paket | Anteil |
   | --- | --- |
   | Material | 35 % |
   | Kochzutaten | 20 % |
   | Proviant | 20 % |
   | Getränke | 15 % |
   | Süßes und Alkohol | 10 % |

   Bei etwa 290 Füll-Items sind das rund 60 Mahlzeiten und 45 Getränke über das ganze
   Spiel. Bei 60 bis 100 Spieltagen ist das etwa eine Mahlzeit jeden zweiten Tag, also
   eine Hilfe und kein Ersatz für Jagen und Kochen.
3. **Nichts Wertvolles als Füller.** Kein Kupfer, kein Obsidian, kein Metallschrott, kein
   Seil, kein Stoff, keine Werkzeuge. Diese Dinge kommen nur über Blueprints und
   Ausrüstungs-Exemplare.
4. **Lieferbremse.** Ein `!release` eines anderen Spielers kann hunderte Items auf einmal
   schicken. Die Grenzen dafür:
   - Fundstücke landen höchstens **10 pro Spielstunde** auf dem Boden.
   - Der Rest wartet im „Briefkasten“ im F8-Fenster und lässt sich dort per Knopf
     abholen.
   - Blueprints, Skills und Perks kommen immer sofort.
5. **Regler in der YAML:** `fundstuecke: wenig / normal / viel`.
   - `wenig`: halbe Mengen; ein Teil der Pakete wird zu kleinen „Erinnerungsstücken“
     (Muschel, hübscher Stein) ohne Nutzen.
   - `viel`: anderthalbfache Mengen.
   - Standard: `normal`.
6. **Keine dauerhaften Boni über das Original hinaus.** Skills bleiben bei 3 × 25 je Skill,
   genau wie die Skill-Perks des Spiels. Perks sind nur solche, die das Spiel selbst hat.
7. **Die Logik verlässt sich nie auf Füller, Nützliches oder Ausbau-Sets.** Ein Seed ist
   also auch ohne Glück bei den Fundstücken schaffbar.

### 3.4 So fühlt sich ein Spiel an (Beispiel, Solo, alles an)

| Phase | Was du tust | Was du ungefähr bekommst |
| --- | --- | --- |
| Tag 1 bis 10 | Strand, Fischen, erste Forschung, erste Gebiete | 10 bis 15 Blueprints, 2 bis 3 Skill-Stufen, Material und Wasser |
| Tag 10 bis 40 | Hütte, Kochtopf, Jagd, Gerichte, Erkunden | Progressive Unterkunft 1 bis 2, Werkzeuglinien, Kochzutaten, 1 bis 2 Perks |
| Tag 40+ | Kupfer, Floß, Logbuch abschließen | späte Blueprints, Ausbau-Set, Stauraum, Rest der Skills |

---

## 4. Reihenfolge der Umsetzung

1. **Phase A: neue Checks.**
   - Ein gemeinsamer Mechanismus „zum ersten Mal X“.
   - Darauf aufbauend Fischen, Jagd, Kochen und Anbau.
   - Dazu Fundstücke mit Lieferbremse und Briefkasten (ersetzen die heutigen Vorräte).
2. **Phase B: Charakter und Logbuch.**
   - Der Archipelago-Reisende mit Logbuch (zuerst im F8-Fenster), inklusive Erkunden.
   - Option `character`.
3. **Phase C: Progressive-Linien und Upgrades.**
   - Unterkunft, Bett und Lager als Progressive-Linien.
   - Ausbau-Set und Stauraum-Erweiterung.
4. **Phase D: Extras und Fallen.**
   - Erfolge, Überlebenstage, Tiere.
   - Fallen mit Deckel.

Jede Phase bekommt ihre eigene Version, Tests im echten Spiel und eine aktualisierte
spielbare Kopie.

---

## 5. Offene Entscheidungen

1. **Progressive-Linien:** Unterkunft, Bett und Lager standardmäßig an? Weitere Linien,
   z. B. Äxte (Stein → Feuerstein → Kupfer), auch?
2. **Ausbau-Set:** 2 Stück im Pool. Auch ohne gebauten Vorgänger einlösbar (baut dann die
   erste Stufe)? Vorschlag: nein, nur ein echter Ausbau.
3. **Logbuch:** Sollen die Multiworld-Aufgaben (Items anderer Spieler) Checks sein, oder
   nur Determination geben? Vorschlag: nur Determination, weil man sie nicht selbst in der
   Hand hat.
4. **Fundstücke:** Passt die Aufteilung in 3.3? Sollen sie im Gebiet liegen, in dem du
   gerade bist, oder immer an deinem Lager (Hütte)? Vorschlag: aktuelles Gebiet, in einer
   Hütte im Innenraum.
5. **Fallen:** Ist die Liste in 1.5 so in Ordnung?
