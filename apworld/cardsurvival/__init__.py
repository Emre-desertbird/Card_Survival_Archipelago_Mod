"""Card Survival: Tropical Island APWorld: research, build and explore to find your blueprints, skills and perks."""

import json
from importlib import resources

from BaseClasses import Item, ItemClassification, Location, LocationProgressType, Region, Tutorial
from worlds.AutoWorld import WebWorld, World

from .access import Registry
from .options import CardSurvivalOptions

DATA = json.loads(resources.files(__package__).joinpath("registry.json").read_text(encoding="utf-8"))
REGISTRY = Registry(DATA)
GAME = DATA["game_name"]
FILLERS = DATA["filler"]               # category -> find names
FILLER_SHARES = DATA["filler_shares"]  # category -> share of the filler pool
CLASSIFICATIONS = {"progression": ItemClassification.progression, "useful": ItemClassification.useful,
                   "filler": ItemClassification.filler, "trap": ItemClassification.trap}


class CardSurvivalItem(Item):
    game = GAME


class CardSurvivalLocation(Location):
    game = GAME


class CardSurvivalWebWorld(WebWorld):
    game = GAME
    theme = "jungle"
    tutorials = [Tutorial(
        "Setup Guide", "Install the Card Survival: Tropical Island randomizer and connect to a game.",
        "English", "setup_en.md", "setup/en", ["Card Survival Archipelago Mod contributors"],
    )]


GROUPS = {"blueprint": "Blueprints", "skill": "Skills", "perk": "Perks", "newperk": "New Perks", "find": "Finds",
          "upgrade": "Hut Upgrades", "trap": "Traps"}
LOCATION_GROUPS = {"research": "Research", "build": "Build", "visit": "Visit", "fish": "Fishing",
                   "hunt": "Hunting", "cook": "Cooking", "farm": "Farming", "logbook": "Logbook",
                   "extra": "Extras"}
# Optional location kinds and the option that turns each on.
OPTIONAL_KINDS = {"fish": "fishing_checks", "hunt": "hunting_checks", "cook": "cooking_checks",
                  "farm": "farming_checks", "extra": "extra_checks"}
PASSTHROUGH = ["exclude_challenge_checks", "finds_amount", "character_lock", "logbook", "new_perks",
               "hut_upgrades", "trap_percentage", "trap_limit",
               *OPTIONAL_KINDS.values()]


class CardSurvivalWorld(World):
    """Stranded on a tropical island, you survive as usual, but your knowledge is shuffled.
    Researching, building and exploring send checks; blueprints, skills and perks arrive as items."""

    game = GAME
    web = CardSurvivalWebWorld()
    options_dataclass = CardSurvivalOptions
    options: CardSurvivalOptions
    hidden = True  # not listed on the website until a full manual playthrough is done (docs/plan.md)
    item_name_to_id = {entry["name"]: entry["ap_id"] for entry in DATA["items"]}
    location_name_to_id = {entry["name"]: entry["ap_id"] for entry in DATA["locations"]}
    item_name_groups = {group: {entry["name"] for entry in DATA["items"] if entry["kind"] == kind}
                        for kind, group in GROUPS.items()}
    location_name_groups = {group: {entry["name"] for entry in DATA["locations"] if entry["kind"] == kind}
                            for kind, group in LOCATION_GROUPS.items()}
    required_client_version = (0, 6, 7)
    # Universal Tracker rebuilds this world from the seed's slot data (no YAML).
    ut_can_gen_without_yaml = True

    def generate_early(self) -> None:
        # Universal Tracker regenerates from slot data instead of a YAML.
        passthrough = getattr(self.multiworld, "re_gen_passthrough", {}).get(self.game)
        for name in PASSTHROUGH:
            if passthrough and name in passthrough:
                getattr(self.options, name).value = int(passthrough[name])

    def enabled_kinds(self) -> list[str]:
        """Location kinds in this slot: the base ones plus the optional ones switched on."""
        base = [kind for kind in LOCATION_GROUPS if kind not in OPTIONAL_KINDS and kind != "logbook"]
        kinds = base + [kind for kind, option in OPTIONAL_KINDS.items() if getattr(self.options, option)]
        if self.options.logbook and self.options.character_lock:
            kinds.append("logbook")   # the logbook is written for the Archipelago character
        return kinds

    def included_locations(self) -> list[dict]:
        kinds = set(self.enabled_kinds())
        # A logbook task counting checks of a kind exists only when that kind does.
        return [entry for entry in DATA["locations"] if entry["kind"] in kinds
                and set(entry.get("requires_kinds", [])) <= kinds]

    @staticmethod
    def interpret_slot_data(slot_data: dict) -> dict:
        return slot_data

    def create_regions(self) -> None:
        REGISTRY.validate()
        menu = Region("Menu", self.player, self.multiworld)
        island = Region("Island", self.player, self.multiworld)
        self.multiworld.regions.extend([menu, island])
        menu.connect(island)
        for entry in self.included_locations():
            location = CardSurvivalLocation(self.player, entry["name"], entry["ap_id"], island)
            location.access_rule = REGISTRY.location_rules[entry["name"]].as_ap_rule(self.player)
            if entry.get("challenge") and self.options.exclude_challenge_checks:
                location.progress_type = LocationProgressType.EXCLUDED
            island.locations.append(location)
        for entry in DATA["events"]:
            location = CardSurvivalLocation(self.player, entry["name"], None, island)
            location.access_rule = REGISTRY.event_rules[entry["name"]].as_ap_rule(self.player)
            location.place_locked_item(CardSurvivalItem(entry["name"], ItemClassification.progression,
                                                        None, self.player))
            island.locations.append(location)

    def create_items(self) -> None:
        optional = {"newperk": self.options.new_perks, "upgrade": self.options.hut_upgrades}
        pool = [self.create_item(entry["name"]) for entry in DATA["items"] for _ in range(entry["quantity"])
                if optional.get(entry["kind"], True)]
        free = len(self.included_locations()) - len(pool)
        # Traps replace some filler: a share of it, never more than the limit.
        traps = min(self.options.trap_limit.value, round(free * self.options.trap_percentage.value / 100))
        pool += [self.create_item(self.random.choice(DATA["traps"])) for _ in range(traps)]
        pool += [self.create_filler() for _ in range(free - traps)]
        self.multiworld.itempool.extend(pool)

    def create_item(self, name: str) -> CardSurvivalItem:
        return CardSurvivalItem(name, CLASSIFICATIONS[REGISTRY.items[name]["classification"]],
                                self.item_name_to_id[name], self.player)

    def get_filler_item_name(self) -> str:
        if self.options.finds_amount == self.options.finds_amount.option_less and self.random.random() < 0.5:
            return DATA["keepsake"]
        categories = list(FILLER_SHARES)
        category = self.random.choices(categories, weights=[FILLER_SHARES[name] for name in categories])[0]
        return self.random.choice(FILLERS[category])

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
            "exclude_challenge_checks": bool(self.options.exclude_challenge_checks),
            "finds_amount": int(self.options.finds_amount),
            "location_kinds": self.enabled_kinds(),
            "character_lock": bool(self.options.character_lock),
            "new_perks": bool(self.options.new_perks),
            "hut_upgrades": bool(self.options.hut_upgrades),
            **{option: int(getattr(self.options, option)) for option in OPTIONAL_KINDS.values()},
        }
