"""Build apworld/cardsurvival/registry.json from the game data dump (.local/dump).

Registry 1 randomizes blueprints:
  item      "Blueprint: X"  - makes blueprint X buildable
  location  "Research - X"  - finishing the research of X in the blueprint screen

Logic comes from the game data. A research check needs the cards that reveal the
blueprint in vanilla (its unlock conditions); a card made by a blueprint needs
that blueprint and, through "Can Build" events, everything it is built from.
Cards with no blueprint behind them (found, harvested, cooked) and skill levels
count as always obtainable for now; docs/plan.md lists that audit as milestone 3.
"""

from __future__ import annotations

import argparse
import json
from collections import Counter
from pathlib import Path

from common import AUDITED_BUILD_ID, DUMP, WORLD

GAME = "Card Survival: Tropical Island"
ITEM_BASE = 1694420000        # Steam app id * 1000
LOCATION_BASE = 1694430000
# Filler items: everyday supplies handed out once. Cards are named by their game object
# name and resolved to UniqueIDs from the dump, so a typo or a game update fails loudly.
FILLERS = [
    ("Washed-Up Supplies", 9000, {"Coconut": 1, "Sticks": 2, "Fibers": 2}),
    ("Fresh Coconuts", 9001, {"Coconut": 3}),
    ("Bundle of Sticks", 9002, {"Sticks": 3, "StickLong": 1}),
    ("Stones and Flint", 9003, {"Stone": 2, "Flint": 1}),
    ("Water Ration", 9004, {"WaterRation": 1}),
    ("Rope and Fibers", 9005, {"Rope": 1, "Fibers": 2}),
]
EXCLUDED = {"BP_TestBox1", "BlueprintModelTest"}      # developer test blueprints
GOAL_BLUEPRINT = "Bp_Raft"                             # provisional goal route, see docs/plan.md


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

    made_by: dict[str, list[str]] = {}
    for name in sorted(blueprints):
        if name in character_only:
            continue
        for result in refs(blueprints[name].get("BlueprintResult"), "DroppedCard"):
            made_by.setdefault(result, []).append(name)

    title_count = Counter(text(blueprints[name].get("CardName")) for name in blueprints)
    def title(name: str) -> str:
        label = text(blueprints[name].get("CardName")) or name
        return label if title_count[label] == 1 else f"{label} ({name.removeprefix('Bp_')})"
    item_name = lambda name: f"Blueprint: {title(name)}"
    can_build = lambda name: f"Can Build {title(name)}"

    def card_rule(card: str):
        """Being able to hold this card: free unless only blueprints make it."""
        makers = made_by.get(card)
        return simplify("any", [{"event": can_build(maker)} for maker in makers]) if makers else True

    events, used_items = [], set()
    for name in sorted(set(randomized) | set(starting)):
        needs = [] if name in starting else [{"item": item_name(name)}]
        for stage in blueprints[name].get("BlueprintStages", []):
            for element in (stage or {}).get("RequiredElements", []):
                element = element or {}
                if "RequiredCard" in element:
                    needs.append(card_rule(element["RequiredCard"]["$ref"]))
                elif "RequiredTabGroup" in element:
                    members = groups.get(element["RequiredTabGroup"]["$ref"], [])
                    needs.append(simplify("any", [card_rule(member) for member in members]) if members else True)
        events.append({"name": can_build(name), "blueprint": name, "rule": simplify("all", needs)})

    locations = []
    for index, name in enumerate(randomized):
        card = blueprints[name]
        # Unlock conditions that share an ObjectiveName are alternatives (see CardUnlockConditions
        # in the game code); differently named ones must all hold. Skill, tag, time and objective
        # conditions count as always possible in this registry version.
        alternatives: dict[str, list] = {}
        for index_, condition in enumerate(card.get("CardsOnBoard") or []):
            condition = condition or {}
            rule = card_rule(condition["Card"]["$ref"]) if "Card" in condition else True
            alternatives.setdefault(condition.get("ObjectiveName") or f"#card{index_}", []).append(rule)
        for key in ("TagsOnBoard", "StatValues", "TimeValues", "CompletedObjectives"):
            for index_, condition in enumerate(card.get(key) or []):
                alternatives.setdefault((condition or {}).get("ObjectiveName") or f"#{key}{index_}", []).append(True)
        reveal = [simplify("any", options) for options in alternatives.values()]
        locations.append({"key": name, "name": f"Research - {title(name)}", "ap_id": LOCATION_BASE + index,
                          "unique_id": card["UniqueID"], "research_days": card.get("BlueprintUnlockSunsCost", 0),
                          "vanilla_item": item_name(name), "hint": text(card.get("UnlockConditionsDesc")),
                          "rule": simplify("all", reveal)})
    events.append({"name": "Victory", "rule": {"event": can_build(GOAL_BLUEPRINT)}})

    def atoms(rule):
        if isinstance(rule, dict):
            if "item" in rule:
                yield rule["item"]
            for child in rule.get("all", []) + rule.get("any", []):
                yield from atoms(child)
    # An item is progression when a check or the goal depends on it, directly or through events.
    event_rules = {event["name"]: event["rule"] for event in events}
    def event_atoms(rule):
        if isinstance(rule, dict):
            if "event" in rule:
                yield rule["event"]
            for child in rule.get("all", []) + rule.get("any", []):
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

    items = [{"key": name, "name": item_name(name), "ap_id": ITEM_BASE + index, "quantity": 1, "kind": "blueprint",
              "unique_id": blueprints[name]["UniqueID"],
              "classification": "progression" if item_name(name) in used_items else "useful"}
             for index, name in enumerate(randomized)]
    for filler_name, offset, contents in FILLERS:
        missing = [card for card in contents if card not in cards]
        if missing:
            raise SystemExit(f"Filler {filler_name!r} names unknown cards: {missing}")
        items.append({"key": "filler_" + filler_name.lower().replace(" ", "_").replace("-", "_"), "name": filler_name,
                      "ap_id": ITEM_BASE + offset, "quantity": 0, "kind": "filler", "classification": "filler",
                      "contents": [{"card": card, "unique_id": cards[card]["UniqueID"], "count": count}
                                   for card, count in contents.items()]})

    registry = {"game_name": GAME, "registry_version": 2, "audited_build_id": AUDITED_BUILD_ID,
                "audited_game_version": manifest["game_version"], "goal_event": "Victory",
                "starting_blueprints": [title(name) for name in starting],
                "character_only_blueprints": [title(name) for name in character_only],
                "filler": [name for name, _, _ in FILLERS], "items": items, "locations": locations, "events": events}
    target = WORLD / "registry.json"
    rendered = json.dumps(registry, indent=1, ensure_ascii=False) + "\n"
    if args.check:
        if not target.is_file() or target.read_text(encoding="utf-8") != rendered:
            raise SystemExit("registry.json is out of date; run tools/build_registry.py")
    else:
        target.write_text(rendered, encoding="utf-8", newline="\n")
    print(json.dumps({"registry": str(target), "items": len(items) - len(FILLERS), "locations": len(locations),
                      "progression": sum(entry["classification"] == "progression" for entry in items),
                      "useful": sum(entry["classification"] == "useful" for entry in items),
                      "starting": len(starting), "character_only": len(character_only)}))


if __name__ == "__main__":
    main()
