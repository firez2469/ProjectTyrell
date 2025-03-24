# Project Voskgardia
Welcome to Tyrell! This is a game I've been wantin to develop for a while now, but never quite had the will to go through with it! The goal is to develop a simple little strategy game allowing you and friends to take the mantle of the leaders at the brink of the Third Age. Navigate the political turmoil of the era, form alliances, build industry, prepare your forces and engage in a conflict that will the world of Tyrell and spawn the third age of the world.

## Insiprations for this Project
- **Hearts of Iron IV:** Though perhaps designed for a very different world and era, HOI4 and Victoria 3 introduce concepts like national focuses, irregular tile based maps and the idea of building up industry to support the military. It also introduces the concept of asymmetrical warfare where on nation may be far stronger than the rest or function entirely different than the rest, requiring opponents to both understand their weaknessess and the strengths of their opponents.
- **Civilization:** Introduces concepts of research and a simpler combat system than paradox games. Simplifying armies into generalized units. Utilizing simple passive abilities/buffs/levels to specify how effective units are.


## Development Setup
**Unity Version:** 6000.0.35f1

1. Upon installation open the `SampleScene.scene.`
2. Click on MapGenerator object and click `Generate`. Should take a few seconds-minute to load (depending on tile count) then click on `Place`. This will place all the tiles. In future versions we will commit to a tile count setup and this step wont be necessary.

## Current Project Focus (Phase 1)
1. Finish the map view:
- Lay out tiles over a 3D map such that is matches land/sea over the map of Tyrell. (nearly complete).
- Allow for tile selection via Raycasting.
- Create UI to detail information about a tile.
- Create Camera controls to view tiles and loop (similar to that of HOI4).

2. Create core back-end:
- Resources
- Units
- Buildings
- Tiles
- Nation
- Bind these back-end systems into the map.

3. Create visual tints for map:
- Tint for nations
- Tint for alliances
- Tint for Resources?
- Create UI for tints.
- Allow right click over tint for nation information.
