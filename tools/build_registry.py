"""Build apworld/cardsurvival/registry.json from the game data dump (.local/dump).

Registry 1 randomizes blueprints:
  item      "Blueprint: X"  - makes blueprint X buildable
  location  "Research - X"  - finishing the research of X in the blueprint screen

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
from collections import Counter
from pathlib import Path

import card_graph
from card_graph import Need
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
# The ending the logic guarantees. Every ending wins in the game; this one is fully modelled by
# cards (raft, sailing, rescue). Home depends on comfort and days survived, Weston on its quest
# line and Mermaid on the character, so the logic does not rely on them (docs/logic.md).
GOAL_ENDING = "Event_ShipEscape"


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

    title_count = Counter(text(blueprints[name].get("CardName")) for name in blueprints)
    def title(name: str) -> str:
        label = text(blueprints[name].get("CardName")) or name
        return label if title_count[label] == 1 else f"{label} ({name.removeprefix('Bp_')})"
    item_name = lambda name: f"Blueprint: {title(name)}"
    can_build = lambda name: f"Can Build {title(name)}"

    graph = card_graph.build(load)
    blueprint_needs = {name: card_graph.build_needs(card, graph.tab_groups)
                       for name, card in cards.items() if card.get("CardType") == "Blueprint"}
    free = card_graph.Reach(graph, blueprint_needs, set(starting))
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
        locations.append({"key": name, "name": f"Research - {title(name)}", "ap_id": LOCATION_BASE + index,
                          "unique_id": card["UniqueID"], "research_days": card.get("BlueprintUnlockSunsCost", 0),
                          "vanilla_item": item_name(name), "hint": text(card.get("UnlockConditionsDesc")),
                          "rule": rule})
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
            item = [] if name in starting else [{"item": item_name(name)}]
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

    registry = {"game_name": GAME, "registry_version": 3, "audited_build_id": AUDITED_BUILD_ID,
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
