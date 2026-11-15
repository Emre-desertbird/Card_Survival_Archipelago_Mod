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

    def test_at_least(self):
        rule = parse({"atleast": 2, "of": [{"item": "A"}, {"item": "B"}, {"item": "C"}]})
        self.assertTrue(rule.evaluate(lambda name, count: name in {"A", "C"}))
        self.assertFalse(rule.evaluate(lambda name, count: name == "B"))

    def test_bad_rules_fail_loudly(self):
        for raw in ({"itm": "A"}, {"item": "A", "count": 0}, {"any": []}, {"all": [], "any": []}, "A", False,
                    {"atleast": 3, "of": [{"item": "A"}]}, {"atleast": 0, "of": [{"item": "A"}]}):
            with self.assertRaises(ValueError, msg=repr(raw)):
                parse(raw)


class RegistryTests(unittest.TestCase):
    def setUp(self):
        self.registry = Registry(DATA)

    def test_registry_is_valid(self):
        self.registry.validate()

    def test_one_blueprint_item_per_research_check(self):
        blueprints = [entry for entry in DATA["items"] if entry["kind"] == "blueprint"]
        research = [entry for entry in DATA["locations"] if entry["kind"] == "research" and not entry["teaches"]]
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
        self.assertLessEqual({entry["kind"] for entry in DATA["locations"]},
                             {"research", "build", "visit", "fish", "hunt", "cook", "farm", "logbook"})
        self.assertLessEqual({entry["kind"] for entry in DATA["items"]}, {"blueprint", "skill", "perk", "newperk", "find"})

    def test_new_perks_are_small_and_never_logic(self):
        perks = [entry for entry in DATA["items"] if entry["kind"] == "newperk"]
        self.assertEqual(sum(entry["quantity"] for entry in perks), 10)
        for entry in perks:
            self.assertEqual(entry["classification"], "useful", entry["name"])
            self.assertLessEqual(entry["quantity"], 3, entry["name"])

    def test_first_time_checks_watch_cards(self):
        watched = set()
        for entry in DATA["locations"]:
            if entry["kind"] in ("fish", "hunt", "cook", "farm", "logbook"):
                self.assertTrue(entry["watch"], entry["name"])
                for part in entry["watch"]:
                    if "cards" in part:
                        self.assertLessEqual(part["count"], len(part["cards"]))
                        watched.update(part["cards"])
        # A find never completes a check by itself.
        for entry in DATA["items"]:
            if entry["kind"] == "find":
                ids = {part["unique_id"] for part in entry["contents"]} | {part.get("liquid_id") for part in entry["contents"]}
                self.assertFalse(ids & watched, entry["name"])

    def test_logbook_gives_enough_determination(self):
        # The Home ending needs 4000 Determination; the logbook replaces the journal's rewards,
        # so even with only the base check kinds it must give at least that much.
        tasks = [entry for entry in DATA["locations"] if entry["kind"] == "logbook"]
        base_only = sum(entry["determination"] for entry in tasks
                        if set(entry["requires_kinds"]) <= {"research", "build", "visit"})
        self.assertGreaterEqual(base_only, 4000)
        for entry in tasks:
            self.assertTrue(entry["watch"] and entry["chapter"], entry["name"])
            for part in entry["watch"]:
                self.assertEqual(len(part.keys() & {"cards", "actions", "checks"}), 1, entry["name"])

    def test_archipelago_character_is_known(self):
        self.assertEqual(DATA["archipelago_character"]["name"], "NormalGuy")
        self.assertTrue(DATA["archipelago_character"]["unique_id"] and DATA["determination_stat"])

    def test_finds_are_cheap_and_shares_add_up(self):
        self.assertEqual(sum(DATA["filler_shares"].values()), 100)
        valuable = {"Rope", "CopperSheet", "Obsidian", "MetalScrap", "ClothLarge", "Canteen", "Waterskin", "Jerrycan"}
        for entry in DATA["items"]:
            if entry["kind"] == "find":
                self.assertFalse({part["card"] for part in entry["contents"]} & valuable, entry["name"])
                for part in entry["contents"]:
                    if "liquid" in part:
                        self.assertEqual(part["card"], "CoconutShell", entry["name"])

    def test_challenge_checks_leave_room_for_important_items(self):
        important = sum(entry["quantity"] for entry in DATA["items"] if entry["classification"] != "filler")
        standard = sum(not entry["challenge"] for entry in DATA["locations"])
        self.assertLessEqual(important, standard)

    def test_survival_basics_teach_and_send_a_check(self):
        basics = [entry for entry in DATA["locations"] if entry["kind"] == "research" and entry["teaches"]]
        self.assertEqual(len(basics), len(DATA["survival_basics"]))
        names = {entry["name"] for entry in DATA["items"]}
        for entry in basics:
            self.assertIsNone(entry["vanilla_item"])
            self.assertNotIn(entry["name"].replace("Research - ", "Blueprint: "), names)
        # Building them needs no item at all.
        for title in DATA["survival_basics"]:
            event = next(event for event in DATA["events"] if event["name"] == f"Can Build {title}") \
                if any(event["name"] == f"Can Build {title}" for event in DATA["events"]) else None
            if event:
                self.assertNotIn('"item"', json.dumps(event["rule"]))

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
