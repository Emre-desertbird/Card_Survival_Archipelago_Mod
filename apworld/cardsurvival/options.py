"""Player options. Optional features default off so the base experience stays close to the original game."""

from dataclasses import dataclass

from Options import Choice, DefaultOnToggle, PerGameCommonOptions, Range, Toggle


class ExcludeChallengeChecks(DefaultOnToggle):
    """Long checks (research taking 3 or more days, construction taking 12 or more hours) never
    hold progression or useful items, only filler. Turn off to allow anything there."""
    display_name = "Exclude Challenge Checks"


class FishingChecks(Toggle):
    """The first catch of each kind of fish and seafood is a check ("Catch - X")."""
    display_name = "Fishing Checks"


class HuntingChecks(Toggle):
    """The first carcass of each kind of animal is a check ("Hunt - X")."""
    display_name = "Hunting Checks"


class CookingChecks(Toggle):
    """The first time each cooking pot or coconut shell dish exists is a check ("Cook - X")."""
    display_name = "Cooking Checks"


class FarmingChecks(Toggle):
    """A few planting milestones are checks: first crop, three and six different crops, first tree,
    first mushroom bed ("Plant - X")."""
    display_name = "Farming Checks"


class ExtraChecks(Toggle):
    """Extra checks: achievements of the game that no other check covers (carve five figurines,
    find a shipwreck, hatch an egg, ...), keeping animals, and surviving 7, 14, 30, 60 and 100 days
    (60 and 100 are challenge checks)."""
    display_name = "Extra Checks"


class HutUpgrades(Toggle):
    """Adds 2 Hut Upgrade Kits and 3 Storage Extensions as useful items. Use them in the F8 panel
    while you stand in a hut: a kit turns a Shed into a Mud Hut or a Mud Hut into a Stone Hut and
    keeps everything inside; a storage extension adds 25% space to the hut or cellar."""
    display_name = "Hut Upgrades"


class TrapPercentage(Range):
    """How many of the filler items become mild traps (a light wound at worst: an abrasion, a
    bruise, a scratch, a rain shower, mosquitoes, dirt, a fright). At most one trap per in-game
    day, none at sea. 0 turns traps off."""
    display_name = "Trap Percentage"
    range_start = 0
    range_end = 100
    default = 0


class TrapLimit(Range):
    """The most traps a seed can hold for you, even at 100%."""
    display_name = "Trap Limit"
    range_start = 0
    range_end = 30
    default = 10


class CharacterLock(DefaultOnToggle):
    """New Archipelago games always start as Normal Guy, the game's neutral character (no perks,
    no starting kit, the Beach). The logic is made for him. Turn off to play any character;
    the logbook then has no checks."""
    display_name = "Character Lock"


class Logbook(Toggle):
    """The Archipelago logbook: 44 tasks for Normal Guy in six chapters (stranded, survival, the
    island, craft, multiworld, departure). Each task is a check and gives Determination like the
    normal journal, which it replaces. Needs Character Lock."""
    display_name = "Logbook"


class NewPerks(Toggle):
    """New perks the base game does not have, as useful items: Progressive Pack Mule (carry
    more, 3 levels), Nimble Hands (faster building), Pathfinder (faster travel), Progressive
    Careful Hands (tools wear slower, 2 levels), Progressive Inquisitive Mind (faster research,
    2 levels), Pantry Keeper (food spoils slower). The logic never needs them."""
    display_name = "New Perks"


class FindsAmount(Choice):
    """How generous the finds (filler) are. Finds are long-lasting food, drinks, cooking ingredients
    and common materials left on the ground where you are.
    less: half of them are worthless keepsakes instead. more: 50% larger amounts."""
    display_name = "Finds Amount"
    option_less = 0
    option_normal = 1
    option_more = 2
    default = 1


@dataclass
class CardSurvivalOptions(PerGameCommonOptions):
    exclude_challenge_checks: ExcludeChallengeChecks
    fishing_checks: FishingChecks
    hunting_checks: HuntingChecks
    cooking_checks: CookingChecks
    farming_checks: FarmingChecks
    finds_amount: FindsAmount
    character_lock: CharacterLock
    logbook: Logbook
    new_perks: NewPerks
    extra_checks: ExtraChecks
    hut_upgrades: HutUpgrades
    trap_percentage: TrapPercentage
    trap_limit: TrapLimit
