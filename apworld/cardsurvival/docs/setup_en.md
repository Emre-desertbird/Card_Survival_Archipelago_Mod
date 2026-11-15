# Card Survival: Tropical Island — development setup

This world is an early development build. Seeds can be generated, but the game
plugin does not connect to Archipelago yet, so it cannot be played as a randomizer.
See the project README for the current status and the roadmap.

## Generating a seed

1. Put `cardsurvival.apworld` into Archipelago's `custom_worlds` folder and restart Archipelago.
2. Use a YAML like this:

   ```yaml
   name: YourName
   game: "Card Survival: Tropical Island"
   "Card Survival: Tropical Island":
     death_link: false
   ```

3. Generate as usual.
