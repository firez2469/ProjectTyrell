# Project Tyrell
Welcome to Tyrell! Where magic slowly fades into the annals of history as the power of steam and industry push a world into a future of ash and war. The goal is simple. To develop a grand strategy game allowing multiple players to take the mantle of leaders on the brink of the Third Age. Navigate the political turmoil of the era, form alliances, build industry, prepare your forces, and engage in a conflict that will stretch the world of Tyrell and bring forth the dawn of the third age of the world.

## Inspirations for this Project
- **Hearts of Iron IV:** Though perhaps designed for a very different world and era, HOI4 and Europa Universalis 4 introduce concepts like national focuses, irregular tile based maps and the idea of building up industry to support the military. It also introduces the concept of asymmetrical warfare where one nation may be far stronger than the rest or function entirely different than the rest. This dynamic requiring opponents to both understand the weaknessess and the strengths of their opponents.
- **Civilization Series:** Introduces concepts of research and a simpler combat system than paradox games. Simplifying armies into generalized units. Utilizing simple passive abilities/buffs/levels to specify how effective units are.


## Development Setup
**Unity Version:** 6000.0.35f1

1. Upon installation open the `Map1.scene`. The map should be generated.
2. You may click on tiles in the game to see detail about them, feel free to edit.
3. In order to create a new random tile map based on the borders for the world of Tyrell, open the `Tools` menu in terminal and run `main.py`. Feel free to modify settings withing:
    - `vornoi.py` to control the number/concentration of tiles. I have made custom generation maps based on the maps on the tyrell website to detect land/sea and to weigh point generation around areas of interest. (The weighing can be found in `tools/maps/TyrellMapWeights.png`).

## Current Project Focus (Phase 1)
✅=Complete<br>
💻=In development<br>
🟥=Incomplete<br>

1. Finish the map view:✅

- ✅ Lay out tiles over a 3D map such that is matches land/sea over the map of Tyrell.
- ✅ Allow for tile selection via Raycasting.
- ✅ Create UI to detail information about a tile.
- ✅ Create Camera controls to view tiles.
- ✅ Create basic UI layout for game.

2. Create Second Layer Structures
- 💻 Tile Enviornment (land, sea, coast, city, mountain, hills, forest)
- 💻 Tile Population
- 🟥 Nation Definition in game. (name, id, leader, token nation focus tree)
- 💻 Tile controller (via nation id).

3. Develop dynamic asset loading system:
- 🟥 Load tile information by Json
- 🟥 Load image/icon art via Json
- 🟥 Load nation information by Json

4. Create DB for info loading
- 🟥 DB communication (creation, deletion, push/pull)
- 🟥 Live updates to tile information
- 🟥 Live updates to nation

5. Enhance Tile definition
- 🟥 Buildings on Tiles
- 🟥 Units on Tiles

...

## Licensing
[MIT Open Source License](./LICENSE)

