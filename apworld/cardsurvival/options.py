"""Player options. Optional features default off so the base experience stays close to the original game."""

from dataclasses import dataclass

from Options import DeathLink, PerGameCommonOptions


@dataclass
class CardSurvivalOptions(PerGameCommonOptions):
    death_link: DeathLink
