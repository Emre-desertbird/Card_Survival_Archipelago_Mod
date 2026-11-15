"""Player options. Optional features default off so the base experience stays close to the original game."""

from dataclasses import dataclass

from Options import DefaultOnToggle, PerGameCommonOptions


class ExcludeChallengeChecks(DefaultOnToggle):
    """Long checks (research taking 3 or more days, construction taking 12 or more hours) never
    hold progression or useful items, only filler. Turn off to allow anything there."""
    display_name = "Exclude Challenge Checks"


@dataclass
class CardSurvivalOptions(PerGameCommonOptions):
    exclude_challenge_checks: ExcludeChallengeChecks
