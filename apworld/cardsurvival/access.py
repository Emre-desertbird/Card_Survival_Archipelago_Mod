"""Access rules read from registry.json, shared by the APWorld, the tools and the tests.

A rule is JSON: {"item": name, "count": n}, {"event": name}, {"all": [...]},
{"any": [...]}, {"atleast": n, "of": [...]} (n of the rules hold), or true for "always possible". Unknown shapes fail loudly so a
typo can never silently become free access.
"""

from dataclasses import dataclass
from typing import Callable, Mapping


@dataclass(frozen=True)
class Rule:
    kind: str
    name: str = ""
    count: int = 1
    children: tuple["Rule", ...] = ()

    def evaluate(self, has: Callable[[str, int], bool]) -> bool:
        if self.kind in ("item", "event"):
            return has(self.name, self.count)
        if self.kind == "all":
            return all(child.evaluate(has) for child in self.children)
        if self.kind == "any":
            return any(child.evaluate(has) for child in self.children)
        if self.kind == "atleast":
            found = 0
            for child in self.children:
                if child.evaluate(has):
                    found += 1
                    if found >= self.count:
                        return True
            return False
        raise ValueError(f"Unknown access rule: {self.kind}")

    def atoms(self):
        if self.kind in ("item", "event"):
            yield self
        for child in self.children:
            yield from child.atoms()

    def as_ap_rule(self, player: int):
        return lambda state: self.evaluate(lambda name, count: state.has(name, player, count))


FREE = Rule("all")


def parse(raw) -> Rule:
    if raw is True or raw is None:
        return FREE
    if isinstance(raw, dict) and len(raw.keys() - {"count"}) == 1:
        if "item" in raw or "event" in raw:
            kind = "item" if "item" in raw else "event"
            count = raw.get("count", 1)
            if not isinstance(raw[kind], str) or not isinstance(count, int) or isinstance(count, bool) or count < 1:
                raise ValueError(f"Invalid access rule: {raw!r}")
            return Rule(kind, raw[kind], count)
        if ("all" in raw or "any" in raw) and "count" not in raw:
            kind = "all" if "all" in raw else "any"
            children = tuple(parse(child) for child in raw[kind])
            if kind == "any" and not children:
                raise ValueError("An empty 'any' rule can never be satisfied")
            return Rule(kind, children=children)
    if isinstance(raw, dict) and raw.keys() == {"atleast", "of"}:
        count, children = raw["atleast"], tuple(parse(child) for child in raw["of"])
        if not isinstance(count, int) or isinstance(count, bool) or count < 1 or count > len(children):
            raise ValueError(f"Invalid access rule: {raw!r}")
        return Rule("atleast", count=count, children=children)
    raise ValueError(f"Invalid access rule: {raw!r}")


class Registry:
    """Validated view of registry.json."""

    def __init__(self, data: dict):
        self.data = data
        self.items = {entry["name"]: entry for entry in data["items"]}
        self.locations = {entry["name"]: entry for entry in data["locations"]}
        self.events = {entry["name"]: entry for entry in data["events"]}
        self.location_rules = {name: parse(entry.get("rule")) for name, entry in self.locations.items()}
        self.event_rules = {name: parse(entry.get("rule")) for name, entry in self.events.items()}

    def validate(self) -> None:
        data = self.data
        for group, key in (("items", "name"), ("items", "ap_id"), ("locations", "name"), ("locations", "ap_id"),
                           ("events", "name")):
            values = [entry[key] for entry in data[group]]
            if len(values) != len(set(values)):
                raise ValueError(f"Duplicate {key} in {group}")
        if self.items.keys() & self.events.keys() or data["goal_event"] not in self.events:
            raise ValueError("Event names collide with items, or the goal event is missing")
        fillers = [name for names in data["filler"].values() for name in names] + [data["keepsake"]]
        if any(self.items.get(name, {}).get("classification") != "trap" for name in data.get("traps", [])):
            raise ValueError("Every trap name must be a trap item in the item list")
        if not all(data["filler"].values()) or any(self.items.get(name, {}).get("classification") != "filler"
                                                   for name in fillers):
            raise ValueError("Every filler name must be a filler item in the item list")
        for rule in list(self.location_rules.values()) + list(self.event_rules.values()):
            for atom in rule.atoms():
                if atom.kind == "event":
                    if atom.name not in self.events:
                        raise ValueError(f"Unknown event in a rule: {atom.name}")
                    continue
                definition = self.items.get(atom.name)
                if definition is None or definition["classification"] != "progression":
                    raise ValueError(f"A rule needs a non-progression or unknown item: {atom.name}")
                if atom.count > definition["quantity"]:
                    raise ValueError(f"A rule needs more copies than exist: {atom.name}")
        pool = sum(entry["quantity"] for entry in data["items"])
        if pool > len(data["locations"]):
            raise ValueError(f"More items ({pool}) than locations ({len(data['locations'])})")
        everything = {name: entry["quantity"] for name, entry in self.items.items()}
        locations, events = self.sweep(everything)
        if set(locations) != set(self.locations) or data["goal_event"] not in events:
            missing = sorted(set(self.locations) - set(locations))
            raise ValueError(f"Not everything is reachable with all items: {missing[:5]}")

    def sweep(self, inventory: Mapping[str, int]):
        """Which locations and events are reachable with these items (fixed point over events)."""
        collected: set[str] = set()
        has = lambda name, count: name in collected if name in self.events else inventory.get(name, 0) >= count
        while True:
            new = {name for name, rule in self.event_rules.items() if name not in collected and rule.evaluate(has)}
            if not new:
                break
            collected |= new
        return [name for name, rule in self.location_rules.items() if rule.evaluate(has)], collected

    def spheres(self) -> list[list[str]]:
        """Play order if every check held its own vanilla blueprint; proves there is no deadlock."""
        inventory: dict[str, int] = {}
        done: set[str] = set()
        result = []
        while True:
            reachable = [name for name in self.sweep(inventory)[0] if name not in done]
            if not reachable:
                return result
            result.append(sorted(reachable))
            done.update(reachable)
            for name in reachable:
                grants = self.locations[name].get("vanilla_item")
                if grants:
                    inventory[grants] = inventory.get(grants, 0) + 1
