"""Build apworld/cardsurvival/registry.json from the game data dump (.local/dump).

Items:
  "Blueprint: X"        makes blueprint X buildable; for equipment also one finished copy
  "Progressive <Skill>" raises a skill (3 per skill, +25 each = one skill perk)
  "Perk: <Name>"        a helpful passive perk of the game (no cards, no blueprints)
  "Find: X"             filler: long-lasting food, drinks, ingredients or common materials,
                        left on the ground of the current area (docs/plan_erweiterung.md)
Locations:
  "Research - X"        finishing the research of X in the blueprint screen
  "Build - X"           finishing blueprint X for the first time (crafting or construction)
  "Visit - <Area>"      entering an area for the first time
  optional "first time" checks, each a YAML option (off by default):
  "Catch - X"           the first fish or seafood of a kind
  "Hunt - X"            the first carcass of an animal
  "Cook - X"            the first time a cooking pot or coconut shell dish exists
  "Plant - ..."         planting milestones
Long checks are marked "challenge"; by default they never hold progression or useful items.

Logic comes from the game data (tools/card_graph.py, explained in docs/logic.md).
Every way to get a card is a recipe: actions, tools, cooking, time, exploring,
travelling, fights, events and building blueprints. Cards reachable without any
Archipelago item are simply "true"; every other card becomes an event "Have X"
whose rule lists its recipes. A research check needs what reveals the blueprint
in vanilla (cards, tags, raised stats); the goal needs a real ending.
"""

from __future__ import annotations

import argparse
import json
import re
from collections import Counter
from pathlib import Path

import card_graph
from card_graph import Need
from common import AUDITED_BUILD_ID, DUMP, WORLD

GAME = "Card Survival: Tropical Island"
ITEM_BASE = 1694420000        # Steam app id * 1000
LOCATION_BASE = 1694430000
# Finds (filler): things the base game already keeps for a long time, so the mod changes no
# durability. Cheap only: no copper, rope, cloth or tools, liquids only in a coconut shell.
# (category, name, offset, contents); a content is {card: count} or ("container", "liquid").
# Category shares of the filler pool are FIND_SHARES (docs/plan_erweiterung.md, 6.2).
FINDS = [
    ("material", "Find: Bundle of Sticks", 9000, {"Sticks": 3}),
    ("material", "Find: Long Sticks", 9001, {"StickLong": 2}),
    ("material", "Find: Stones", 9002, {"Stone": 2}),
    ("material", "Find: Fibers", 9003, {"Fibers": 3}),
    ("material", "Find: Palm Fronds", 9004, {"PalmFronds": 2}),
    ("material", "Find: Clay", 9005, {"Clay": 1}),
    ("ingredient", "Find: Salt", 9010, {"Salt": 1}),
    ("ingredient", "Find: Dried Chilies", 9011, {"ChiliesDried": 1}),
    ("ingredient", "Find: Dried Ginger", 9012, {"GingerDried": 1}),
    ("ingredient", "Find: Coconuts", 9013, {"Coconut": 2}),
    ("ingredient", "Find: Honey", 9014, ("CoconutShell", "LQ_Honey")),
    ("ingredient", "Find: Fat", 9015, {"Fat": 1}),
    ("provision", "Find: Dried Boar Meat", 9020, {"BoarMeatDried": 1}),
    ("provision", "Find: Dried Goat Meat", 9021, {"GoatMeatDried": 1}),
    ("provision", "Find: Dried Macaque Meat", 9022, {"MacaqueMeatDried": 1}),
    ("provision", "Find: Cured Meat", 9023, {"MeatSaltedDried": 1}),
    ("provision", "Find: Boiled Eggs", 9024, {"EggBoiled": 2}),
    ("provision", "Find: Goat Cheese", 9025, {"Cheese": 1}),
    ("drink", "Find: Fresh Water", 9030, ("CoconutShell", "LQ_Water")),
    ("drink", "Find: Ginger Tea", 9031, ("CoconutShell", "LQ_GingerTea")),
    ("drink", "Find: Jasmine Tea", 9032, ("CoconutShell", "LQ_JasmineTea")),
    ("treat", "Find: Candied Ginger", 9040, {"CandiedGinger": 1}),
    ("treat", "Find: Chocolate", 9041, {"Chocolate": 1}),
    ("treat", "Find: Palm Wine", 9042, ("CoconutShell", "LQ_PalmWine")),
    ("treat", "Find: Mead", 9043, ("CoconutShell", "LQ_Mead")),
    ("keepsake", "Keepsake: Pretty Seashells", 9050, {"SeashellsPretty": 1}),
]
FIND_SHARES = {"material": 35, "ingredient": 20, "provision": 20, "drink": 15, "treat": 10}
# Optional "first time" checks. Hand-made lists name game objects and are checked against the
# data: a card that does not exist fails loudly, a group nothing can reach is dropped.
# Fish and seafood, one check per kind (meat cut from a fish counts for that fish).
FISHING = [
    ("Goatfish", ["Goatfish"]), ("Parrot Fish", ["ParrotFish"]), ("Bonefish", ["Bonefish", "BonefishMeat"]),
    ("Mudskipper", ["Mudskipper"]), ("Threadfin", ["KingThreadfin", "ThreadfinMeat"]), ("Herring", ["Herring"]),
    ("Grouper", ["GrouperMeat"]), ("Prawns", ["Prawns"]), ("Crab", ["Crab"]), ("Oyster", ["OysterMeat"]),
    ("Sea Urchin", ["UrchinMeat"]), ("Conch", ["ConchMeat"]),
]
# Hunting: the carcass, as the game's own hunting objectives (ObjH5_Hunt*) check it.
HUNTING = [
    ("Boar", ["BoarCarcass"]), ("Piglet", ["BoarCarcassPiglet"]), ("Cobra", ["CobraDead"]),
    ("Goat", ["GoatCarcassFemale", "GoatCarcassMale", "GoatCarcassKid"]), ("Macaque", ["MacaqueCarcass"]),
    ("Monitor Lizard", ["MonitorCarcass"]), ("Partridge", ["PartridgeCarcass", "PartridgeDead", "PartridgeChickDead"]),
    ("Sea Krait", ["SeaKraitDead"]), ("Seagull", ["SeagullCarcass", "SeagullDead"]), ("Shark", ["SharkCarcass"]),
    ("Seahound", ["SeahoundCarcass"]),
]
# Cooking: every dish of the game's two meal tags. Planting: a few milestones, not every crop.
COOKING_TAGS = ["tag_MealCookingpot", "tag_MealCoconutShell"]
CROP_TAG, CROP_EXCLUDED = "tag_Crop", {"CropPlotEmpty", "CropPlotRuined", "MushroomBedEmpty"}
EXCLUDED = {"BP_TestBox1", "BlueprintModelTest"}      # developer test blueprints
# The ending the logic guarantees. Every ending wins in the game; this one is fully modelled by
# cards (raft, sailing, rescue). Home depends on comfort and days survived, Weston on its quest
# line and Mermaid on the character, so the logic does not rely on them (docs/logic.md).
GOAL_ENDING = "Event_ShipEscape"
# Survival basics (rule 6 in docs/plan.md): their research teaches the recipe as in vanilla
# AND sends its check, so shelter, sleep, light, a water bowl and basic wound care never
# depend on other players. They have no item.
SURVIVAL_BASICS = ["Bp_Shelter", "Bp_Leafbed", "Bp_Torch", "Bp_ClayBowl", "Bp_Splint", "Bp_AshDressing"]
SKILL_COPIES, SKILL_STEP = 3, 25          # the game's skill perks give +75
EXCLUDED_SKILLS = {"Skill_Insight"}       # tied to endings, see docs/plan.md
CHALLENGE_RESEARCH_DAYS = 3               # research taking this many days or more
CHALLENGE_BUILD_HOURS = 12                # construction taking this many in-game hours or more


def text(value) -> str:
    return (value or {}).get("DefaultText", "") if isinstance(value, dict) else ""


def refs(values, key=None) -> list[str]:
    result = []
    for entry in values or []:
        target = entry.get(key) if key and isinstance(entry, dict) else entry
        if isinstance(target, dict) and "$ref" in target:
            result.append(target["$ref"])
    return result


def simplify(kind: str, rules: list):
    """Flatten a list of rules into the smallest equivalent JSON rule."""
    flat = []
    for rule in rules:
        if rule is True:
            if kind == "any":
                return True
            continue
        if rule not in flat:
            flat.append(rule)
    if not flat:
        return True
    return flat[0] if len(flat) == 1 else {kind: flat}


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dump", type=Path, default=DUMP)
    parser.add_argument("--check", action="store_true", help="Fail if registry.json is not up to date")
    args = parser.parse_args()

    load = lambda name: json.loads((args.dump / f"{name}.json").read_text(encoding="utf-8"))
    manifest = json.loads((args.dump / "manifest.json").read_text(encoding="utf-8"))
    cards = {card["$name"]: card for card in load("CardData")}
    groups = {group["$name"]: refs(group.get("IncludedCards")) for group in load("CardTabGroup")}
    gamemode = load("Gamemode")[0]

    blueprints = {name: card for name, card in cards.items()
                  if card.get("CardType") == "Blueprint" and card.get("BlueprintStages") and name not in EXCLUDED}
    starting = sorted(name for name in refs(gamemode.get("BaseAndItemCards")) if name in blueprints)
    # Blueprints with no unlock conditions are only ever handed out by a character's starting kit.
    conditions = ("CardsOnBoard", "TagsOnBoard", "StatValues", "TimeValues", "CompletedObjectives")
    character_only = sorted(name for name, card in blueprints.items()
                            if name not in starting and not any(card.get(key) for key in conditions))
    randomized = sorted(name for name in blueprints if name not in starting and name not in character_only)
    missing = [name for name in SURVIVAL_BASICS if name not in randomized]
    if missing:
        raise SystemExit(f"Survival basics not found among researchable blueprints: {missing}")

    title_count = Counter(text(blueprints[name].get("CardName")) for name in blueprints)
    def title(name: str) -> str:
        label = text(blueprints[name].get("CardName")) or name
        return label if title_count[label] == 1 else f"{label} ({name.removeprefix('Bp_')})"
    item_name = lambda name: f"Blueprint: {title(name)}"
    can_build = lambda name: f"Can Build {title(name)}"

    graph = card_graph.build(load)
    blueprint_needs = {name: card_graph.build_needs(card, graph.tab_groups)
                       for name, card in cards.items() if card.get("CardType") == "Blueprint"}
    for name in SURVIVAL_BASICS:
        # Buildable without an item, once the research is revealed (and done).
        blueprint_needs[name] = blueprint_needs[name] + card_graph.unlock_needs(blueprints[name])
    free = card_graph.Reach(graph, blueprint_needs, set(starting) | set(SURVIVAL_BASICS))
    full = card_graph.Reach(graph, blueprint_needs, set(starting) | set(randomized))
    producers = graph.producers()

    def have(card: str) -> str:
        if card.startswith("stat:"):
            return f"Can Raise {card[5:]}"
        if card.startswith("victory:"):
            return f"Can Play Ending {card[8:]}"
        return f"Have {card}"

    queued_cards: list[str] = []
    queued_builds: list[str] = []
    def need_rule(need: Need):
        """JSON rule for one need, True when free, None when impossible even with every item."""
        if need.kind == "card":
            if need.name in free.cards:
                return True
            if need.name not in full.cards:
                return None
            queued_cards.append(need.name)
            return {"event": have(need.name)}
        if need.kind == "tag":
            return any_rule([Need("card", member) for member in graph.tags.get(need.name, [])])
        if need.kind == "build":
            if need.name in free.built:
                return True
            if need.name not in full.built:
                return None
            queued_builds.append(need.name)
            return {"event": can_build(need.name)}
        return any_rule(need.options)

    def any_rule(needs):
        rules = [rule for rule in (need_rule(need) for need in needs) if rule is not None]
        return simplify("any", rules) if rules else None

    def all_rule(needs):
        rules = [need_rule(need) for need in needs]
        return None if any(rule is None for rule in rules) else simplify("all", rules)

    locations = []
    for index, name in enumerate(randomized):
        card = blueprints[name]
        rule = all_rule(card_graph.unlock_needs(card))
        if rule is None:
            raise SystemExit(f"The research of {name} can never be revealed")
        days = card.get("BlueprintUnlockSunsCost", 0) or 0
        locations.append({"key": name, "kind": "research", "name": f"Research - {title(name)}",
                          "ap_id": LOCATION_BASE + index, "unique_id": card["UniqueID"], "research_days": days,
                          "vanilla_item": None if name in SURVIVAL_BASICS else item_name(name),
                          "teaches": name in SURVIVAL_BASICS, "hint": text(card.get("UnlockConditionsDesc")),
                          "challenge": days >= CHALLENGE_RESEARCH_DAYS, "rule": rule})

    # Building: the first time a blueprint is finished (an AP item never counts, see game_plugin/Checks.cs).
    for index, name in enumerate(sorted(set(starting) | set(randomized))):
        card = blueprints[name]
        hours = (card.get("BuildingDaytimeCost", 0) or 0) * len(card.get("BlueprintStages") or []) / 4
        locations.append({"key": name, "kind": "build", "name": f"Build - {title(name)}",
                          "ap_id": LOCATION_BASE + 1000 + index, "unique_id": card["UniqueID"],
                          "build_hours": hours, "challenge": hours >= CHALLENGE_BUILD_HOURS,
                          "rule": need_rule(Need("build", name))})

    # Areas: entering one for the first time. Only areas the logic can reach (not character-only ones).
    areas = sorted(name for name, card in cards.items() if card.get("CardType") == "Environment" and name in full.cards)
    area_titles = Counter(text(cards[name].get("CardName")) for name in areas)
    for index, name in enumerate(areas):
        label = text(cards[name].get("CardName")) or name
        if area_titles[label] > 1:
            label = f"{label} ({name.removeprefix('Env_')})"
        locations.append({"key": name, "kind": "visit", "name": f"Visit - {label}",
                          "ap_id": LOCATION_BASE + 2000 + index, "unique_id": cards[name]["UniqueID"],
                          "challenge": False, "rule": need_rule(Need("card", name))})
    # Optional "first time" checks: a card of a group exists for the first time in this game.
    def card_names(names: list[str]) -> list[str]:
        unknown = [name for name in names if name not in cards]
        if unknown:
            raise SystemExit(f"Unknown cards in a check list: {unknown}")
        return names

    def at_least(count: int, groups: list[list[str]]):
        """At least `count` of the groups (each: any of its cards) can be had."""
        rules = [rule for rule in (any_rule([Need("card", name) for name in group]) for group in groups)
                 if rule is not None]
        if len(rules) < count:
            return None
        if count == 1:
            return simplify("any", rules)
        free_count = sum(rule is True for rule in rules)
        if free_count >= count:
            return True
        rest = [rule for rule in rules if rule is not True]
        return {"atleast": count - free_count, "of": rest}

    watched: list[tuple[str, str, int, list[str], int]] = []     # kind, name, ap offset, cards, count
    for index, (label, names) in enumerate(FISHING):
        watched.append(("fish", f"Catch - {label}", 3000 + index, card_names(names), 1))
    for index, (label, names) in enumerate(HUNTING):
        watched.append(("hunt", f"Hunt - {label}", 3100 + index, card_names(names), 1))
    dishes = sorted({name for tag in COOKING_TAGS for name in graph.tags.get(tag, [])},
                    key=lambda name: text(cards[name].get("CardName")) or name)
    for index, name in enumerate(dishes):
        watched.append(("cook", f"Cook - {text(cards[name].get('CardName')) or name}", 3200 + index, [name], 1))
    crops = sorted(name for name in graph.tags.get(CROP_TAG, []) if name not in CROP_EXCLUDED)
    mushrooms = [name for name in crops if name.startswith("MushroomBed")]
    plants = [name for name in crops if name not in mushrooms]
    trees = [name for name in plants if "Tree" in name]
    for index, (label, names, count) in enumerate([
            ("First Crop", plants, 1), ("Three Different Crops", plants, 3), ("Six Different Crops", plants, 6),
            ("First Tree", trees, 1), ("First Mushroom Bed", mushrooms, 1)]):
        watched.append(("farm", f"Plant - {label}", 3300 + index, names, count))
    for kind, name, offset, names, count in watched:
        rule = at_least(count, [[card] for card in names]) if count > 1 else any_rule([Need("card", card) for card in names])
        if rule is None:
            continue   # nothing can produce it in this game mode (e.g. Mermaid-only animals)
        locations.append({"key": name, "kind": kind, "name": name, "ap_id": LOCATION_BASE + offset,
                          "watch": [{"cards": [cards[card]["UniqueID"] for card in names], "count": count}],
                          "challenge": False, "rule": rule})

    goal = need_rule(Need("card", "victory:" + GOAL_ENDING))
    if goal is None or goal is True:
        raise SystemExit(f"The goal ending {GOAL_ENDING} is unreachable or needs no item: {goal}")

    events: list[dict] = [{"name": "Victory", "rule": goal}]
    done: set[str] = set()
    while queued_cards or queued_builds:
        if queued_builds:
            name = queued_builds.pop()
            if can_build(name) in done:
                continue
            done.add(can_build(name))
            rule = all_rule(blueprint_needs[name])
            item = [] if name in starting or name in SURVIVAL_BASICS else [{"item": item_name(name)}]
            events.append({"name": can_build(name), "blueprint": name, "rule": simplify("all", item + [rule])})
            continue
        card = queued_cards.pop()
        if have(card) in done:
            continue
        done.add(have(card))
        options = [rule for rule in (all_rule(recipe.needs) for recipe in producers.get(card, [])) if rule is not None]
        events.append({"name": have(card), "card": card, "rule": simplify("any", options)})
    events.sort(key=lambda event: event["name"])
    used_items = set()

    def children(rule):
        return rule.get("all", []) + rule.get("any", []) + rule.get("of", [])

    def atoms(rule):
        if isinstance(rule, dict):
            if "item" in rule:
                yield rule["item"]
            for child in children(rule):
                yield from atoms(child)
    # An item is progression when a check or the goal depends on it, directly or through events.
    event_rules = {event["name"]: event["rule"] for event in events}
    def event_atoms(rule):
        if isinstance(rule, dict):
            if "event" in rule:
                yield rule["event"]
            for child in children(rule):
                yield from event_atoms(child)
    needed_events, queue = set(), [name for entry in locations for name in event_atoms(entry["rule"])] + ["Victory"]
    while queue:
        current = queue.pop()
        if current not in needed_events:
            needed_events.add(current)
            queue.extend(event_atoms(event_rules[current]))
    for current in needed_events:
        used_items.update(atoms(event_rules[current]))
    # Events nothing depends on would only be unreachable clutter (their blueprint is not progression).
    events = [event for event in events if event["name"] in needed_events]

    def equipment(name: str) -> list[dict]:
        """Results the game itself treats as equipment (weapon, armor or an equipment slot)."""
        result = []
        for drop in blueprints[name].get("BlueprintResult") or []:
            card = cards.get(((drop or {}).get("DroppedCard") or {}).get("$ref"))
            if card and card.get("CardType") == "Item" and (card.get("IsWeapon") or card.get("IsArmor")
                                                            or card.get("EquipmentTags")):
                result.append({"card": card["$name"], "unique_id": card["UniqueID"]})
        return result

    items = []
    for index, name in enumerate(randomized):
        if name in SURVIVAL_BASICS:
            continue
        entry = {"key": name, "name": item_name(name), "ap_id": ITEM_BASE + index, "quantity": 1, "kind": "blueprint",
                 "unique_id": blueprints[name]["UniqueID"],
                 "classification": "progression" if item_name(name) in used_items else "useful"}
        if equipment(name):
            entry["equipment"] = equipment(name)
        items.append(entry)

    # Skills the player can train (some card raises them), except ending-related ones.
    stats = {stat["$name"]: stat for stat in load("GameStat")}
    skills = sorted(name for name in stats if name.startswith("Skill_") and name not in EXCLUDED_SKILLS
                    and "stat:" + name in full.cards)
    for index, name in enumerate(skills):
        items.append({"key": name, "name": "Progressive " + re.sub(r"(?<=[a-z])(?=[A-Z])", " ", text(stats[name].get("GameName")) or name[6:]),
                      "ap_id": ITEM_BASE + 3000 + index, "quantity": SKILL_COPIES, "kind": "skill",
                      "stat_id": stats[name]["UniqueID"], "amount": SKILL_STEP, "classification": "useful"})

    # Helpful vanilla perks that only change stats passively (no cards, no blueprints, no start changes).
    perks = sorted((perk for perk in load("CharacterPerk")
                    if perk.get("PassiveStatModifiers") and (perk.get("DifficultyRating") or 0) < 0
                    and not any(perk.get(key) for key in ("AddedCards", "EquippedCards", "OverrideEnvironment",
                                                          "OverrideWeather", "StartingStatModifiers"))),
                   key=lambda perk: perk["$name"])
    for index, perk in enumerate(perks):
        items.append({"key": perk["$name"], "name": f"Perk: {text(perk.get('PerkName')) or perk['$name']}",
                      "ap_id": ITEM_BASE + 4000 + index, "quantity": 1, "kind": "perk",
                      "perk_id": perk["UniqueID"], "classification": "useful"})
    # A find must never complete a "first time" check by itself.
    watched_cards = {card for _, _, _, names, _ in watched for card in names}
    for category, find_name, offset, contents in FINDS:
        if isinstance(contents, tuple):
            container, liquid = contents
            names = [container, liquid]
            parts = [{"card": container, "unique_id": cards.get(container, {}).get("UniqueID"), "count": 1,
                      "liquid": liquid, "liquid_id": cards.get(liquid, {}).get("UniqueID")}]
        else:
            names = list(contents)
            parts = [{"card": card, "unique_id": cards.get(card, {}).get("UniqueID"), "count": count}
                     for card, count in contents.items()]
        missing = [card for card in names if card not in cards]
        if missing:
            raise SystemExit(f"Find {find_name!r} names unknown cards: {missing}")
        if watched_cards & set(names):
            raise SystemExit(f"Find {find_name!r} would complete a check by itself: {watched_cards & set(names)}")
        items.append({"key": "find_" + re.sub(r"\W+", "_", find_name.split(": ", 1)[1].lower()), "name": find_name,
                      "ap_id": ITEM_BASE + offset, "quantity": 0, "kind": "find", "category": category,
                      "classification": "filler", "contents": parts})

    registry = {"game_name": GAME, "registry_version": 6, "audited_build_id": AUDITED_BUILD_ID,
                "audited_game_version": manifest["game_version"], "goal_event": "Victory",
                "starting_blueprints": [title(name) for name in starting],
                "survival_basics": [title(name) for name in SURVIVAL_BASICS],
                "character_only_blueprints": [title(name) for name in character_only],
                "filler": {category: [name for kind, name, _, _ in FINDS if kind == category] for category in FIND_SHARES},
                "filler_shares": FIND_SHARES, "keepsake": next(name for kind, name, _, _ in FINDS if kind == "keepsake"),
                "items": items, "locations": locations, "events": events}
    target = WORLD / "registry.json"
    rendered = json.dumps(registry, indent=1, ensure_ascii=False) + "\n"
    if args.check:
        if not target.is_file() or target.read_text(encoding="utf-8") != rendered:
            raise SystemExit("registry.json is out of date; run tools/build_registry.py")
    else:
        target.write_text(rendered, encoding="utf-8", newline="\n")
    print(json.dumps({"registry": str(target), "item_kinds": Counter(entry["kind"] for entry in items),
                      "pool": sum(entry["quantity"] for entry in items), "locations": len(locations),
                      "location_kinds": Counter(entry["kind"] for entry in locations),
                      "challenge": sum(entry["challenge"] for entry in locations),
                      "progression": sum(entry["classification"] == "progression" for entry in items),
                      "useful": sum(entry["classification"] == "useful" for entry in items),
                      "starting": len(starting), "character_only": len(character_only)}))


if __name__ == "__main__":
    main()
