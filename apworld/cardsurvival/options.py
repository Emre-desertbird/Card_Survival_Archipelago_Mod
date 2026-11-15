"""Player options. Optional features default off so the base experience stays close to the original game."""

from dataclasses import dataclass

from Options import Choice, DefaultOnToggle, PerGameCommonOptions, Toggle


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
