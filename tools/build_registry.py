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
# The Archipelago character: the game's neutral character (no perks, no kit, starts on the Beach).
# It is the base model of custom characters and not in the character list of the game mode.
ARCHIPELAGO_CHARACTER = "NormalGuy"
# The Archipelago logbook (docs/plan_erweiterung.md, 1.4): new tasks for the Archipelago character.
# It replaces the rewards of the normal journal, so its Determination adds up like the original
# (the Home ending needs 4000). Sources:
#   ("objective", name)        the conditions of a game objective (cards, tags, actions)
#   ("cards", [names], n)      n different of these cards existed
#   ("tags", [tags])           any card with one of these tags existed
#   ("checks", [kinds], n)     n checks of these kinds sent (empty list: research, build, visit)
#   ("explore",)               one task per "Explore the ..." objective of the game
LOGBOOK = [
    ("Stranded", "First Refreshment", 50, ("objective", "Obj1_EatDrinkCoconut")),
    ("Stranded", "Spark", 50, ("objective", "Obj2_MakeCampfire")),
    ("Stranded", "Sharp Mind", 50, ("objective", "Obj1_MakeSharpenedStone")),
    ("Stranded", "Water Hole", 50, ("objective", "Obj3_FindPuddle")),
    ("Stranded", "A Roof Overhead", 50, ("objective", "Obj3_MakeShelter")),
    ("Survival", "Clean Water", 100, ("objective", "ObjT2_BoilWater")),
    ("Survival", "Smokehouse", 100, ("cards", "*Smoked", 1)),
    ("Survival", "First Aid", 100, ("objective", "Obj_WoundDressing")),
    ("Survival", "Cool Head", 100, ("cards", ["ClayPotCoolerUndeployed", "ClayPotCoolerOff", "ClayPotCoolerOn"], 1)),
    ("Survival", "A Real Meal", 100, ("tags", ["tag_MealCookingpot", "tag_MealCoconutShell"])),
    ("The Island", None, 100, ("explore",)),
    ("Craft", "Potter", 250, ("objective", "Obj4_MakeKiln")),
    ("Craft", "Weaver", 250, ("objective", "Obj4_MakeLoom")),
    ("Craft", "Ropemaker", 250, ("cards", ["Rope"], 1)),
    ("Craft", "Copper Age", 250, ("objective", "Ach_MakeCopperTool")),
    ("Craft", "Angler", 250, ("cards", ["FishingRod"], 1)),
    ("Multiworld", "Catch of the Day", 100, ("checks", ["fish"], 10)),
    ("Multiworld", "Hunter's Tally", 100, ("checks", ["hunt"], 5)),
    ("Multiworld", "Head Chef", 100, ("checks", ["cook"], 8)),
    ("Multiworld", "Scholar", 100, ("checks", ["research"], 25)),
    ("Multiworld", "Master Builder", 100, ("checks", ["build"], 25)),
    ("Multiworld", "Wanderer", 100, ("checks", ["visit"], 15)),
    ("Multiworld", "Messenger I", 100, ("checks", [], 50)),
    ("Multiworld", "Messenger II", 100, ("checks", [], 100)),
    ("Multiworld", "Messenger III", 100, ("checks", [], 200)),
    ("Departure", "Set Sail", 500, ("objective", "Obj6_MakeRaft")),
]
BASE_KINDS = ["research", "build", "visit"]
# Extras (option extra_checks): achievements of the game that no other check already covers and
# that do not ask the player to get hurt, keeping animals, and days survived.
# (label, source, challenge); sources as in LOGBOOK, plus ("day", n).
EXTRAS = [
    ("Carve Five Figurines", ("objective", "Ach_CarveFigurines"), False),
    ("Use Dynamite", ("objective", "Ach_Dynamite", ["DynamiteOn", "DynamiteOff"]), False),   # the action needs dynamite
    ("Eat a Prepared Meal", ("objective", "Ach_EatAdvancedDish"), False),
    ("Explore the Cave System", ("objective", "Ach_ExploreCaves"), True),
    ("Find a Shipwreck", ("objective", "Ach_FindShipwreck"), False),
    ("A Goat Gives Birth", ("objective", "Ach_GoatBirth"), False),
    ("Hatch a Partridge Egg", ("objective", "Ach_HatchEgg"), False),
    ("Make a Macaque Friend", ("objective", "Ach_MacaqueFriend"), False),
    ("Make a Pearl Necklace", ("objective", "Ach_MakePearlNecklace"), False),
    ("Make a Friend", ("objective", "Ach_MakeWeston"), False),
    ("Weston Talks Back", ("objective", "Ach_WestonTalk"), False),
    ("Defeat the Hunter", ("objective", "Ach_DefeatHunter"), False),
    ("Keep a Goat", ("cards", ["GoatTiedFemale", "GoatTiedMale", "GoatTiedFemaleLactating", "GoatTiedKid",
                               "GoatEnclosureFemale", "GoatEnclosureMale", "GoatEnclosureLactating", "GoatEnclosureKid"], 1), False),
    ("Keep Partridges", ("cards", ["PartridgeFemaleEnclosure", "PartridgeMaleEnclosure"], 1), False),
    ("Keep a Boar", ("cards", ["BoarTiedFemale", "BoarTiedMale", "BoarTiedPiglet",
                               "BoarEnclosureFemale", "BoarEnclosureMale", "BoarEnclosurePiglet"], 1), False),
    ("Survive 7 Days", ("day", 7), False),
    ("Survive 14 Days", ("day", 14), False),
    ("Survive 30 Days", ("day", 30), False),
    ("Survive 60 Days", ("day", 60), True),
    ("Survive 100 Days", ("day", 100), True),
]
# Traps (options trap_percentage, trap_limit): mild only, the worst is a light wound.
# (name, offset, effect): ("card", name) adds the card (a wound, or a weather that replaces the
# current one), ("stat", name, amount) changes a stat once.
TRAPS = [
    ("Trap: Abrasion", 9100, ("card", "W_Abrasion")),
    ("Trap: Bruise", 9101, ("card", "W_Bruise")),
    ("Trap: Scratch", 9102, ("card", "W_LizardScratch")),
    ("Trap: Rain Shower", 9103, ("card", "TropicalIsland_LightRain")),
    ("Trap: Mosquito Swarm", 9104, ("stat", "BugBites", 48)),
    ("Trap: Mud and Dirt", 9105, ("stat", "Filth", 100)),
    ("Trap: Fright", 9106, ("stat", "Stress", 30)),
]
TRAP_FREE_AREAS = ["Env_Raft"]        # at sea: no traps
# Hut upgrades (option hut_upgrades). The kit turns the hut you stand in into the next tier;
# each tier is (environment, entrance, explorable, exit). Storage extensions add 25% space to the
# hut or cellar you stand in.
HUT_TIERS = [("Env_Shed", "ShedEntrance", "Shed", "ShedExit"),
             ("Env_MudHut", "MudHutEntrance", "MudHut", "MudHutExit"),
             ("Env_StoneHut", "StoneHutEntrance", "StoneHut", "StoneHutExit")]
STORAGE_AREAS = ["Env_Shed", "Env_MudHut", "Env_StoneHut", "Env_Cellar"]
UPGRADES = [("upgrade_kit", "Hut Upgrade Kit", 6000, 2), ("storage_extension", "Storage Extension", 6001, 3)]
# New perks that the base game does not have (option new_perks, off by default). Small steps,
# so the game stays a survival game (docs/plan_erweiterung.md, 3). (key, name, levels, what one level does)
NEW_PERKS = [
    ("pack_mule", "Progressive Pack Mule", 3, "carried weight counts 10% less per level"),
    ("nimble_hands", "Nimble Hands", 1, "building blueprints takes 33% less time, never under 15 minutes"),
    ("pathfinder", "Pathfinder", 1, "travelling takes 33% less time, never under 15 minutes"),
    ("careful_hands", "Progressive Careful Hands", 2, "tools wear 15% slower per level"),
    ("inquisitive_mind", "Progressive Inquisitive Mind", 2, "research is 15% faster per level"),
    ("pantry_keeper", "Pantry Keeper", 1, "food and drinks spoil 15% slower"),
]
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

    # The Archipelago logbook: each task is a check and gives Determination (see LOGBOOK).
    objectives = {objective["$name"]: objective for objective in load("Objective")}
    logbook_cards: set[str] = set()

    def objective_parts(name: str) -> list[dict]:
        """Watch parts from a game objective: conditions sharing a name are alternatives."""
        objective = objectives[name]
        if objective.get("StatsObjectives") or objective.get("NestedObjectives"):
            raise SystemExit(f"Objective {name} has conditions the logbook cannot watch")
        groups: dict[tuple, list] = {}
        for entry in objective.get("CardsOnBoardObjectives") or []:
            groups.setdefault(("cards", entry.get("ObjectiveName")), []).append(entry["Card"]["$ref"])
        for entry in objective.get("TagsOnBoardObjectives") or []:
            groups.setdefault(("cards", entry.get("ObjectiveName")), []).extend(graph.tags.get(entry["Tag"]["$ref"], []))
        for entry in objective.get("ActionObjectives") or []:
            groups.setdefault(("actions", entry.get("ObjectiveName")), []).append((entry["OnCard"]["$ref"], entry["ActionName"]))
        parts = []
        for (kind, _), values in groups.items():
            if kind == "cards":
                parts.append({"cards": card_names(sorted(set(values))), "count": 1})
            else:
                parts.append({"actions": [{"card": card, "action": action} for card, action in values]})
        return parts

    def part_rule(part: dict, location_rules: dict):
        if "cards" in part:
            return at_least(part["count"], [[card] for card in part["cards"]])
        if "actions" in part:
            return any_rule([Need("card", entry["card"]) for entry in part["actions"]])
        kinds = part["checks"]["kinds"] or BASE_KINDS
        rules = [rule for entry, rule in location_rules.items() if entry in kinds]
        return at_least_rules(part["checks"]["count"], [rule for group in rules for rule in group])

    def at_least_rules(count: int, rules: list):
        if len(rules) < count:
            return None
        free_count = sum(rule is True for rule in rules)
        if free_count >= count:
            return True
        rest = [rule for rule in rules if rule is not True]
        return simplify("any", rest) if count - free_count == 1 else {"atleast": count - free_count, "of": rest}

    rules_by_kind: dict[str, list] = {}
    for entry in locations:
        rules_by_kind.setdefault(entry["kind"], []).append(entry["rule"])
    tasks = []
    for chapter, title_text, determination, source in LOGBOOK:
        if source[0] == "explore":
            for name in sorted(objective for objective in objectives if objective.startswith("ObjExplore_")):
                label = text(objectives[name].get("ObjectiveDescription")) or name
                tasks.append((chapter, label, determination, objective_parts(name), []))
            continue
        if source[0] == "objective":
            parts = objective_parts(source[1])
        elif source[0] == "cards":
            names = source[1] if isinstance(source[1], list) else sorted(
                name for name, card in cards.items() if card.get("CardType") == "Item" and name.endswith(source[1][1:]))
            parts = [{"cards": card_names(names), "count": source[2]}]
        elif source[0] == "tags":
            parts = [{"cards": sorted({name for tag in source[1] for name in graph.tags.get(tag, [])}), "count": 1}]
        else:
            parts = [{"checks": {"kinds": source[1], "count": source[2]}}]
        tasks.append((chapter, title_text, determination, parts, source[1] if source[0] == "checks" else []))
    for index, (chapter, label, determination, parts, requires) in enumerate(tasks):
        rules = [part_rule(part, rules_by_kind) for part in parts]
        if any(rule is None for rule in rules):
            print(f"Logbook task {label!r} is impossible in this game mode; dropped")
            continue
        for part in parts:
            logbook_cards.update(part.get("cards", []))
            if "cards" in part:
                part["cards"] = [cards[card]["UniqueID"] for card in part["cards"]]
            for entry in part.get("actions", []):
                entry["card"] = cards[entry["card"]]["UniqueID"]
        locations.append({"key": f"logbook_{index}", "kind": "logbook", "name": f"Logbook - {label}",
                          "ap_id": LOCATION_BASE + 4000 + index, "chapter": chapter, "determination": determination,
                          "requires_kinds": requires, "watch": parts, "challenge": False,
                          "rule": simplify("all", rules)})

    # Extras: achievements, animals, days survived.
    for index, (label, source, challenge) in enumerate(EXTRAS):
        if source[0] == "day":
            parts, rule = [{"day": source[1]}], True
        else:
            parts = objective_parts(source[1]) if source[0] == "objective" else [{"cards": card_names(source[1]), "count": source[2]}]
            rules = [part_rule(part, rules_by_kind) for part in parts]
            if source[0] == "objective" and len(source) > 2:
                rules.append(any_rule([Need("card", name) for name in card_names(source[2])]))
            if any(rule is None for rule in rules):
                print(f"Extra {label!r} is impossible in this game mode; dropped")
                continue
            rule = simplify("all", rules)
            for part in parts:
                logbook_cards.update(part.get("cards", []))
                if "cards" in part:
                    part["cards"] = [cards[card]["UniqueID"] for card in part["cards"]]
                for entry in part.get("actions", []):
                    entry["card"] = cards[entry["card"]]["UniqueID"]
        locations.append({"key": f"extra_{index}", "kind": "extra", "name": f"Extra - {label}",
                          "ap_id": LOCATION_BASE + 5000 + index, "watch": parts, "challenge": challenge, "rule": rule})

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
    for index, (key, perk_name, levels, effect) in enumerate(NEW_PERKS):
        items.append({"key": key, "name": perk_name, "ap_id": ITEM_BASE + 5000 + index, "quantity": levels,
                      "kind": "newperk", "effect": effect, "classification": "useful"})

    for key, upgrade_name, offset, quantity in UPGRADES:
        items.append({"key": key, "name": upgrade_name, "ap_id": ITEM_BASE + offset, "quantity": quantity,
                      "kind": "upgrade", "classification": "useful"})
    for trap_name, offset, effect in TRAPS:
        target = effect[1]
        if (effect[0] == "card" and target not in cards) or (effect[0] == "stat" and target not in stats):
            raise SystemExit(f"Trap {trap_name!r} names an unknown {effect[0]}: {target}")
        entry = {"key": "trap_" + re.sub(r"\W+", "_", trap_name.split(": ", 1)[1].lower()), "name": trap_name,
                 "ap_id": ITEM_BASE + offset, "quantity": 0, "kind": "trap", "classification": "trap"}
        if effect[0] == "card":
            entry["card_id"] = cards[target]["UniqueID"]
        else:
            entry.update({"stat_id": stats[target]["UniqueID"], "amount": effect[2]})
        items.append(entry)

    # A find must never complete a "first time" check by itself.
    watched_cards = {card for _, _, _, names, _ in watched for card in names} | logbook_cards
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

    registry = {"game_name": GAME, "registry_version": 9, "audited_build_id": AUDITED_BUILD_ID,
                "audited_game_version": manifest["game_version"], "goal_event": "Victory",
                "archipelago_character": {"name": ARCHIPELAGO_CHARACTER, "unique_id": next(
                    character["UniqueID"] for character in load("PlayerCharacter") if character["$name"] == ARCHIPELAGO_CHARACTER)},
                "determination_stat": stats["Determination"]["UniqueID"],
                "hut_tiers": [[cards[name]["UniqueID"] for name in tier] for tier in HUT_TIERS],
                "storage_areas": [cards[name]["UniqueID"] for name in STORAGE_AREAS],
                "trap_free_areas": [cards[name]["UniqueID"] for name in TRAP_FREE_AREAS],
                "traps": [name for name, _, _ in TRAPS],
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
