"""Where every card comes from, read from the game data dump.

A *recipe* is one way to get cards: it lists what you need (cards, a card with some tag,
or being able to build a blueprint) and which cards it produces. Recipes come from:

  - actions on a card (DismantleActions) and travelling (a path "produces" the area it leads to)
  - dragging one card onto another (CardInteractions: tool + target)
  - cooking in a container (CookingRecipes)
  - time passing (spoilage, usage, fuel, progress and special durabilities reaching 0 or full)
  - stat-triggered changes on a card (OnStatsChangeActions), cards dropped when destroyed
  - liquids a card generates or contains, cards an inventory starts with
  - exploring (ExplorationResults), what an area holds when you arrive (DefaultEnvCards)
  - fights and their outcomes (Encounters), self-triggered events (SelfTriggeredActions)
  - building a blueprint (BlueprintResult)

Stat requirements (a skill level, distance sailed, ...) count when the stat starts below the
required value: then some card must be able to raise it (an action, passive effect or
equipment that changes it upwards). Stats that need a character perk are never reachable.

Deliberately ignored (documented in docs/logic.md): how much a stat can rise, durability
ranges, drop chances (any chance counts), and in which area a card lies (a card you have
once counts as available). Requirements that block an action ("Inverted") never add needs.
"""

from __future__ import annotations

from dataclasses import dataclass, field

ACTION_KEYS = {"DismantleActions", "CardInteractions", "CookingRecipes", "OnStatsChangeActions",
               "ExplorationResults", "SpoilageTime", "UsageDurability", "FuelCapacity", "Progress",
               "SpecialDurability1", "SpecialDurability2", "SpecialDurability3", "SpecialDurability4"}
DURABILITIES = ("SpoilageTime", "UsageDurability", "FuelCapacity", "Progress",
                "SpecialDurability1", "SpecialDurability2", "SpecialDurability3", "SpecialDurability4")


def ref(value, kind="CardData"):
    if isinstance(value, dict) and value.get("$type") == kind and "$ref" in value:
        return value["$ref"]
    return None


def refs(values, kind="CardData"):
    return [name for name in (ref(value, kind) for value in values or []) if name]


@dataclass(frozen=True)
class Need:
    kind: str            # "card", "tag", "build" or "any" (one of several needs)
    name: str = ""
    options: tuple = ()


@dataclass
class Recipe:
    needs: tuple
    products: tuple
    via: str


@dataclass
class Graph:
    cards: dict
    tags: dict = field(default_factory=dict)            # tag -> cards having it
    recipes: list = field(default_factory=list)
    roots: set = field(default_factory=set)             # cards everyone starts with or next to
    victory_cards: list = field(default_factory=list)   # cards with an action that ends the game in victory
    # Pseudo-cards: "stat:<GameStat>" = the stat can be raised, "victory:<card>" = that ending can be played.
    tab_groups: dict = field(default_factory=dict)

    def producers(self) -> dict:
        result: dict[str, list[Recipe]] = {}
        for recipe in self.recipes:
            for product in recipe.products:
                result.setdefault(product, []).append(recipe)
        return result


def dropped(collections) -> list[str]:
    """Cards a list of drop collections can produce (any non-zero quantity counts)."""
    result = []
    for collection in collections or []:
        for drop in (collection or {}).get("DroppedCards") or []:
            card = ref((drop or {}).get("DroppedCard"))
            quantity = (drop or {}).get("Quantity") or {}
            if card and max(quantity.get("m_X", 1) or 0, quantity.get("m_Y", 1) or 0) > 0:
                result.append(card)
        liquid = ref(((collection or {}).get("CreatedLiquid") or {}).get("LiquidCard"))
        if liquid:
            result.append(liquid)
    return result


def encounters_of(collections) -> list[str]:
    return [name for name in (ref((c or {}).get("DroppedEncounter"), "Encounter") for c in collections or []) if name]


def action_products(action: dict, encounter_drops: dict) -> list[str]:
    products = dropped(action.get("ProducedCards"))
    for encounter in encounters_of(action.get("ProducedCards")):
        products += encounter_drops.get(encounter, [])
    for key in ("ReceivingCardChanges", "GivenCardChanges"):
        target = ref(((action.get(key) or {}).get("TransformInto")))
        if target:
            products.append(target)
    liquid = ref((action.get("CreatedLiquidInGivenCard") or {}).get("LiquidCard"))
    if liquid:
        products.append(liquid)
    return products


STAT_BASE: dict = {}       # stat -> starting value, filled by build()


def stat_needs(conditions) -> list[Need]:
    """A stat condition needs the stat raised only if it starts below the required value."""
    needs = []
    for condition in conditions or []:
        condition = condition or {}
        stat = ref(condition.get("Stat"), "GameStat")
        low = (condition.get("TriggerRange") or {}).get("x", 0) or 0
        if stat and low > STAT_BASE.get(stat, 0):
            needs.append(Need("card", "stat:" + stat))
    return needs


def action_needs(action: dict) -> list[Need]:
    needs = stat_needs(action.get("RequiredStatValues"))
    for entry in action.get("RequiredCardsOnBoard") or []:
        card = ref((entry or {}).get("TriggerCard"))
        if card and not entry.get("Inverted"):
            needs.append(Need("card", card))
    for entry in action.get("RequiredTagsOnBoard") or []:
        tag = ref((entry or {}).get("TriggerTag"), "CardTag")
        if tag and not (entry or {}).get("Inverted"):
            needs.append(Need("tag", tag))
    return needs


def build(dump_loader) -> Graph:
    cards = {card["$name"]: card for card in dump_loader("CardData")}
    graph = Graph(cards)
    perk_stats = set()
    for stat in dump_loader("GameStat"):
        STAT_BASE[stat["$name"]] = stat.get("BaseValue", 0) or 0
        if (stat.get("BaseRatePerTick") or 0) > 0:
            graph.roots.add("stat:" + stat["$name"])      # rises on its own over time
        if stat.get("RequiredPerks"):
            perk_stats.add(stat["$name"])

    def raised(node) -> list[str]:
        """Pseudo-cards for every stat this part of the data changes upwards."""
        found = []
        def walk(item):
            if isinstance(item, dict):
                stat = ref(item.get("Stat"), "GameStat")
                change = item.get("ValueModifier") or item.get("RateModifier")
                if stat and isinstance(change, dict) and stat not in perk_stats \
                        and max(change.get("x", 0) or 0, change.get("y", 0) or 0) > 0:
                    found.append("stat:" + stat)
                for value in item.values():
                    walk(value)
            elif isinstance(item, list):
                for value in item:
                    walk(value)
        walk(node)
        return found
    for card in cards.values():
        for tag in refs(card.get("CardTags"), "CardTag"):
            graph.tags.setdefault(tag, []).append(card["$name"])
    graph.tab_groups = {group["$name"]: refs(group.get("IncludedCards")) for group in dump_loader("CardTabGroup")}

    encounter_drops = {}
    for encounter in dump_loader("Encounter"):
        drops = []
        def walk(node):
            if isinstance(node, dict):
                drops.extend(refs(node.get("DroppedCards")))
                for value in node.values():
                    walk(value)
            elif isinstance(node, list):
                for value in node:
                    walk(value)
        walk(encounter)
        encounter_drops[encounter["$name"]] = drops

    def add(needs, products, via, source=None):
        products = tuple(dict.fromkeys(list(products) + (raised(source) if source is not None else [])))
        if products:
            graph.recipes.append(Recipe(tuple(needs), products, via))

    for name, card in cards.items():
        here = Need("card", name)
        for index, action in enumerate(card.get("DismantleActions") or []):
            products = action_products(action, encounter_drops)
            if (action.get("VictorySettings") or {}).get("Victory"):
                graph.victory_cards.append(name)
                products.append("victory:" + name)
            add([here] + action_needs(action), products, f"{name}.action{index}", action)
        for index, action in enumerate(card.get("CardInteractions") or []):
            compatible = action.get("CompatibleCards") or {}
            tools = [Need("card", tool) for tool in refs(compatible.get("TriggerCards"))]
            tools += [Need("tag", tag) for tag in refs(compatible.get("TriggerTags"), "CardTag")]
            needs = [here] + action_needs(action)
            if tools:
                needs.append(tools[0] if len(tools) == 1 else Need("any", options=tuple(tools)))
            add(needs, action_products(action, encounter_drops), f"{name}.interaction{index}", action)
        for index, recipe in enumerate(card.get("CookingRecipes") or []):
            ingredients = [Need("card", item) for item in refs(recipe.get("CompatibleCards"))]
            ingredients += [Need("tag", tag) for tag in refs(recipe.get("CompatibleTags"), "CardTag")]
            needs = [here]
            if ingredients:
                needs.append(ingredients[0] if len(ingredients) == 1 else Need("any", options=tuple(ingredients)))
            products = [ref((drop or {}).get("DroppedCard")) for drop in recipe.get("Drops") or []]
            products += dropped(recipe.get("DropsAsCollection"))
            for key in ("IngredientChanges", "CookerChanges"):
                products.append(ref((recipe.get(key) or {}).get("TransformInto")))
            add(needs, [product for product in products if product], f"{name}.cooking{index}", recipe)
        for key in DURABILITIES:
            durability = card.get(key) or {}
            for end in ("OnZero", "OnFull"):
                action = durability.get(end) or {}
                add([here] + action_needs(action), action_products(action, encounter_drops), f"{name}.{key}.{end}", action)
        for index, action in enumerate(card.get("OnStatsChangeActions") or []):
            add([here] + action_needs(action), action_products(action, encounter_drops), f"{name}.onstats{index}", action)
        for index, result in enumerate(card.get("ExplorationResults") or []):
            action = (result or {}).get("Action") or {}
            add([here] + action_needs(action), action_products(action, encounter_drops), f"{name}.explore{index}", action)
        add([here], refs(card.get("DefaultEnvCards")), f"{name}.arrival")
        add([here], refs(card.get("InventorySlots")), f"{name}.inventory")
        add([here], dropped(card.get("DroppedOnDestroy")), f"{name}.destroyed")
        liquids = [ref((effect or {}).get("GeneratedLiquid", {}).get("LiquidCard")) for effect in card.get("PassiveEffects") or []]
        liquids.append(ref((card.get("DefaultLiquidContained") or {}).get("LiquidCard")))
        add([here], [liquid for liquid in liquids if liquid], f"{name}.liquid")
        # Passive stat effects of the card itself (equipment, buildings, areas); for a blueprint,
        # those of the finished building.
        passive = {key: value for key, value in card.items() if key not in ACTION_KEYS}
        add([Need("build", name)] if card.get("CardType") == "Blueprint" else [here], [], f"{name}.passive", passive)
        if card.get("CardType") == "Blueprint":
            add([Need("build", name)], [ref((result or {}).get("DroppedCard")) for result in card.get("BlueprintResult") or []
                                        if ref((result or {}).get("DroppedCard"))], f"{name}.built")

    for event in dump_loader("SelfTriggeredAction"):
        for index, action in enumerate(event.get("Actions") or []):
            add(action_needs(action) + stat_needs(action.get("StatChangeTrigger")),
                action_products(action, encounter_drops), f"{event['$name']}.event{index}", action)

    # Improvements are built in place, in an area that offers them, once their conditions hold.
    for name, card in cards.items():
        for entry in card.get("EnvironmentImprovements") or []:
            improvement = ref((entry or {}).get("Card"))
            if improvement in cards:
                add([Need("card", name)] + build_needs(cards[improvement], graph.tab_groups)
                    + unlock_needs(cards[improvement]), [improvement], f"{name}.improvement.{improvement}")

    gamemode = dump_loader("Gamemode")[0]
    graph.roots.update(refs(gamemode.get("BaseAndItemCards")))
    for key in ("Environment", "Weather"):
        if ref(gamemode.get(key)):
            graph.roots.add(ref(gamemode.get(key)))
    return graph


def build_needs(card: dict, groups: dict) -> list[Need]:
    """What building a blueprint needs besides the blueprint itself."""
    needs = []
    for stage in card.get("BlueprintStages") or []:
        for element in (stage or {}).get("RequiredElements") or []:
            element = element or {}
            if ref(element.get("RequiredCard")):
                needs.append(Need("card", ref(element.get("RequiredCard"))))
            elif ref(element.get("RequiredTabGroup"), "CardTabGroup"):
                members = groups.get(ref(element.get("RequiredTabGroup"), "CardTabGroup"), [])
                if members:
                    needs.append(Need("any", options=tuple(Need("card", member) for member in members)))
            liquid = element.get("RequiredLiquidContent") or {}
            if ref(liquid.get("RequiredLiquid")):
                needs.append(Need("card", ref(liquid.get("RequiredLiquid"))))
            elif ref(liquid.get("RequiredGroup"), "CardTabGroup"):
                members = groups.get(ref(liquid.get("RequiredGroup"), "CardTabGroup"), [])
                if members:
                    needs.append(Need("any", options=tuple(Need("card", member) for member in members)))
    for condition in card.get("BlueprintCardConditions") or []:
        if not (condition or {}).get("Inverted") and ref(condition.get("TriggerCard")):
            needs.append(Need("card", ref(condition.get("TriggerCard"))))
    for condition in card.get("BlueprintTagConditions") or []:
        if not (condition or {}).get("Inverted") and ref(condition.get("TriggerTag"), "CardTag"):
            needs.append(Need("tag", ref(condition.get("TriggerTag"), "CardTag")))
    return needs


def unlock_needs(card: dict) -> list[Need]:
    """Conditions that reveal a card (a research or an improvement). Conditions sharing an
    ObjectiveName are alternatives; differently named ones must all hold."""
    groups: dict[str, list] = {}
    for index, condition in enumerate(card.get("CardsOnBoard") or []):
        condition = condition or {}
        target = ref(condition.get("Card"))
        if target:
            groups.setdefault(condition.get("ObjectiveName") or f"#card{index}", []).append(Need("card", target))
    for index, condition in enumerate(card.get("TagsOnBoard") or []):
        condition = condition or {}
        tag = ref(condition.get("Tag"), "CardTag")
        if tag:
            groups.setdefault(condition.get("ObjectiveName") or f"#tag{index}", []).append(Need("tag", tag))
    for index, condition in enumerate(card.get("StatValues") or []):
        condition = condition or {}
        found = stat_needs([condition.get("StatCondition")])
        # None marks an alternative that is true from the start.
        groups.setdefault(condition.get("ObjectiveName") or f"#stat{index}", []).append(found[0] if found else None)
    needs = []
    for options in groups.values():
        if any(option is None for option in options):
            continue
        needs.append(options[0] if len(options) == 1 else Need("any", options=tuple(options)))
    return needs


class Reach:
    """Fixed point: which cards, tags and buildable blueprints follow from a set of blueprints."""

    def __init__(self, graph: Graph, blueprint_needs: dict, buildable: set):
        self.graph = graph
        self.blueprint_needs = blueprint_needs     # blueprint -> needs (besides owning it)
        self.cards: set = set()
        self.built: set = set()
        changed = True
        self.cards |= graph.roots
        while changed:
            changed = False
            for blueprint, needs in blueprint_needs.items():
                if blueprint in buildable and blueprint not in self.built and all(self.met(need) for need in needs):
                    self.built.add(blueprint)
                    changed = True
            for recipe in graph.recipes:
                if all(product in self.cards for product in recipe.products):
                    continue
                if all(self.met(need) for need in recipe.needs):
                    new = set(recipe.products) - self.cards
                    if new:
                        self.cards |= new
                        changed = True

    def met(self, need: Need) -> bool:
        if need.kind == "card":
            return need.name in self.cards
        if need.kind == "tag":
            return any(member in self.cards for member in self.graph.tags.get(need.name, []))
        if need.kind == "build":
            return need.name in self.built
        return any(self.met(option) for option in need.options)
