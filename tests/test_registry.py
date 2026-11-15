"""Registry and rule checks that need neither the game nor Archipelago. Run: python -m unittest discover tests"""

import json
import sys
import unittest
from pathlib import Path

PROJECT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(PROJECT / "apworld" / "cardsurvival"))

from access import FREE, Registry, parse  # noqa: E402

DATA = json.loads((PROJECT / "apworld" / "cardsurvival" / "registry.json").read_text(encoding="utf-8"))


class RuleTests(unittest.TestCase):
    def test_parse_and_evaluate(self):
        rule = parse({"all": [{"item": "A"}, {"any": [{"item": "B", "count": 2}, {"event": "E"}]}]})
        owned = {"A": 1, "B": 2}
        self.assertTrue(rule.evaluate(lambda name, count: owned.get(name, 0) >= count))
        self.assertFalse(rule.evaluate(lambda name, count: {"A": 1, "B": 1}.get(name, 0) >= count))
        self.assertEqual(parse(True), FREE)

    def test_bad_rules_fail_loudly(self):
        for raw in ({"itm": "A"}, {"item": "A", "count": 0}, {"any": []}, {"all": [], "any": []}, "A", False):
            with self.assertRaises(ValueError, msg=repr(raw)):
                parse(raw)


class RegistryTests(unittest.TestCase):
    def setUp(self):
        self.registry = Registry(DATA)

    def test_registry_is_valid(self):
        self.registry.validate()

    def test_one_blueprint_item_per_research_check(self):
        blueprints = [entry for entry in DATA["items"] if entry["kind"] == "blueprint"]
        research = [entry for entry in DATA["locations"] if entry["kind"] == "research"]
        self.assertEqual(len(blueprints), len(research))
        self.assertEqual({entry["name"] for entry in blueprints}, {entry["vanilla_item"] for entry in research})

    def test_every_shuffled_and_starting_blueprint_has_a_build_check(self):
        builds = {entry["key"] for entry in DATA["locations"] if entry["kind"] == "build"}
        research = {entry["key"] for entry in DATA["locations"] if entry["kind"] == "research"}
        self.assertTrue(research <= builds)
        self.assertEqual(len(builds), len(research) + len(DATA["starting_blueprints"]))

    def test_location_and_item_ids_are_unique_and_kinds_known(self):
        self.assertEqual(len({entry["ap_id"] for entry in DATA["locations"]}), len(DATA["locations"]))
        self.assertEqual(len({entry["ap_id"] for entry in DATA["items"]}), len(DATA["items"]))
        self.assertLessEqual({entry["kind"] for entry in DATA["locations"]}, {"research", "build", "visit"})
        self.assertLessEqual({entry["kind"] for entry in DATA["items"]}, {"blueprint", "skill", "perk", "filler"})

    def test_challenge_checks_leave_room_for_important_items(self):
        important = sum(entry["quantity"] for entry in DATA["items"] if entry["classification"] != "filler")
        standard = sum(not entry["challenge"] for entry in DATA["locations"])
        self.assertLessEqual(important, standard)

    def test_skill_items_add_up_to_one_skill_perk(self):
        for entry in DATA["items"]:
            if entry["kind"] == "skill":
                self.assertEqual(entry["quantity"] * entry["amount"], 75, entry["name"])
                self.assertNotIn("Insight", entry["name"])

    def test_some_checks_are_open_from_the_start(self):
        open_checks, _ = self.registry.sweep({})
        self.assertGreater(len(open_checks), 20)

    def test_vanilla_order_has_no_deadlock(self):
        # If every research check held its own blueprint and every other check held nothing,
        # everything must still be reachable: nothing depends on skills, perks or filler.
        reached = {name for sphere in self.registry.spheres() for name in sphere}
        self.assertEqual(reached, set(self.registry.locations))

    def test_goal_needs_progress(self):
        self.assertNotIn(DATA["goal_event"], self.registry.sweep({})[1])

    def test_starting_blueprints_are_not_shuffled(self):
        names = {entry["name"] for entry in DATA["items"]}
        for title in DATA["starting_blueprints"] + DATA["character_only_blueprints"]:
            self.assertNotIn(f"Blueprint: {title}", names)


if __name__ == "__main__":
    unittest.main()
