"""Card Survival: Tropical Island development APWorld (blueprint research randomizer)."""

import json
from importlib import resources

from BaseClasses import Item, ItemClassification, Location, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .access import Registry
from .options import CardSurvivalOptions

DATA = json.loads(resources.files(__package__).joinpath("registry.json").read_text(encoding="utf-8"))
REGISTRY = Registry(DATA)
GAME = DATA["game_name"]
FILLERS = DATA["filler"]
CLASSIFICATIONS = {"progression": ItemClassification.progression, "useful": ItemClassification.useful,
                   "filler": ItemClassification.filler}


class CardSurvivalItem(Item):
    game = GAME


class CardSurvivalLocation(Location):
    game = GAME


class CardSurvivalWebWorld(WebWorld):
    game = GAME
    theme = "jungle"
    tutorials = [Tutorial(
        "Development Setup Guide", "Set up the Card Survival: Tropical Island development randomizer.",
        "English", "setup_en.md", "setup/en", ["Card Survival Archipelago Mod contributors"],
    )]


class CardSurvivalWorld(World):
    """Stranded on a tropical island, you research blueprints to survive and escape.
    Finishing a blueprint's research sends a check; the blueprints themselves arrive as items."""

    game = GAME
    web = CardSurvivalWebWorld()
    options_dataclass = CardSurvivalOptions
    options: CardSurvivalOptions
    hidden = True  # development build: not listed until a full playthrough is verified
    item_name_to_id = {entry["name"]: entry["ap_id"] for entry in DATA["items"]}
    location_name_to_id = {entry["name"]: entry["ap_id"] for entry in DATA["locations"]}
    required_client_version = (0, 6, 7)
    # Universal Tracker rebuilds this world from the seed's slot data (no YAML).
    ut_can_gen_without_yaml = True

    @staticmethod
    def interpret_slot_data(slot_data: dict) -> dict:
        return slot_data

    def create_regions(self) -> None:
        REGISTRY.validate()
        menu = Region("Menu", self.player, self.multiworld)
        island = Region("Island", self.player, self.multiworld)
        self.multiworld.regions.extend([menu, island])
        menu.connect(island)
        for entry in DATA["locations"]:
            location = CardSurvivalLocation(self.player, entry["name"], entry["ap_id"], island)
            location.access_rule = REGISTRY.location_rules[entry["name"]].as_ap_rule(self.player)
            island.locations.append(location)
        for entry in DATA["events"]:
            location = CardSurvivalLocation(self.player, entry["name"], None, island)
            location.access_rule = REGISTRY.event_rules[entry["name"]].as_ap_rule(self.player)
            location.place_locked_item(CardSurvivalItem(entry["name"], ItemClassification.progression,
                                                        None, self.player))
            island.locations.append(location)

    def create_items(self) -> None:
        pool = [self.create_item(entry["name"]) for entry in DATA["items"] for _ in range(entry["quantity"])]
        pool += [self.create_filler() for _ in range(len(DATA["locations"]) - len(pool))]
        self.multiworld.itempool.extend(pool)

    def create_item(self, name: str) -> CardSurvivalItem:
        return CardSurvivalItem(name, CLASSIFICATIONS[REGISTRY.items[name]["classification"]],
                                self.item_name_to_id[name], self.player)

    def get_filler_item_name(self) -> str:
        return self.random.choice(FILLERS)

    def set_rules(self) -> None:
        self.multiworld.completion_condition[self.player] = (
            lambda state: state.has(DATA["goal_event"], self.player)
        )

    def fill_slot_data(self) -> dict:
        return {
            "development_build": True,
            "registry_version": DATA["registry_version"],
            "audited_build_id": DATA["audited_build_id"],
            "audited_game_version": DATA["audited_game_version"],
        }
