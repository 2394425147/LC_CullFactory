# 1.1.4

## Changed

- Removed the Grand Armory interior from the blacklist, culling issues have been fixed

# 1.1.3

## Fixed

- Imperium's night vision not being visible on the player camera

# 1.1.2

## Fixed

- Added Wesley's upcoming interior to the blacklist, as issues are anticipated at release

# 1.1.1

## Fixed

- The render distance (camera far plane) being set based on where the player previously was when teleporting

# 1.1.0 (requires v50)

## Added

- Tracking of items held by enemies that can enter and leave the interior

## Fixed

- Items becoming invisible if custom moons cause errors while a player enters/leaves the interior

# 1.0.4

## Fixed

- All lights being culled when LethalCompanyVR is active (tentative fix)

# 1.0.3

## Fixed

- Items spawned in the interior through LethalLib remaining invisible permanently, they will now never be culled

# 1.0.2

## Fixed

- Error spam that could occur after exiting to menu, loading into a save again and landing on a moon

# 1.0.1

## Fixed

- The flashlight from [Spectate Enemies](https://thunderstore.io/c/lethal-company/p/AllToasters/SpectateEnemies/) being invisible when viewing an enemy in the interior

# 1.0.0

## Added

- Visibility culling of items inside the dungeon
- Culling of all items outside the dungeon when inside the dungeon and vice versa
- An option that is enabled by default preventing shadowed lights shining through walls when viewing them from afar (i.e. the Mansion's chandeliers in the entry tile)

## Fixed

- Dynamic lights such as flashlights shining through walls
- Faraway shadowed lights on the opposite sides of a walls disappearing and reappearing when moving between tiles
- Puddles in the vanilla Factory interior sometimes disappearing visibly at certain camera angles
- Setting the `[Distance culling] Enabled` option to `false` in-game would set the far planes incorrectly

# 0.9.3

## Fixed

- Better compatibility with DunGen's automatic tile bounds

# 0.9.2

## Changed

- Use fallback portals on SleepsDungeon "School"

## Fixed

- Shadow-casting lights shining through walls when they shouldn't
- Incorrectly inferred room sizes when tile bounds are automatically calculated

# 0.9.1

## Fixed

- Static lights should no longer shine through walls

# 0.9.0

## Changed

- The default/recommended culling frequency is now 0 (Updates every game cycle)
- Light culling is now more accurate

## Fixed

- Portal occlusion culling now accurately reads objects' visibility states from the previous frame.
- Errors occurred when using CullFactory should no longer cause the game to freeze

# 0.8.7

## Fixed

- Removed network-synced object spawn tracking (This should allow door codes to function again)

# 0.8.6

## Changed

- Print warning in console when a tile component is removed unexpectedly

## Fixed

- Tile being entirely excluded from rendering when only partial objects are problematic

# 0.8.5

## Fixed

- Removed benchmarking behaviour when B is pressed

# 0.8.4

## Fixed

- Camera stops rendering when a renderer or light is removed after level generation

# 0.8.3

## Fixed

- Far plane culling not responding to changed settings during gameplay

# 0.8.2

## Fixed

- Mod not being properly initialized when installed manually

# 0.8.1

## Added

- An option to disable compatibility mode on known problematic interiors

## Fixed

- An issue with changing the fallback lists during gameplay

# 0.8.0

## Added

- A compatibility mode for incorrect portals (Enabled by default)

## Fixed

- **Scoopys Variety Mod's interiors** would sometimes appear invisible with portal occlusion culling
  - This is fixed by using the compatibility mode mentioned above
- Shadows flickering on **Scarlet Devil Mansion** with portal occlusion culling
- Depth culling sometimes causing interiors to become entirely invisible

# 0.7.2

## Added

- Options to visualize the portals and tile bounds

## Changed

- The render distance option is now opt-in with the `[Distance culling] Enabled` option

## Fixed

- The entrance room on **Scoopy's Sewer interior** would appear invisible at certain angles
- Depth culling sometimes allowing tiles to remain invisible on cameras
- Camera render distance not changing when entering or leaving on clients, or when using the ship teleporters

# 0.7.1

## Fixed

- Various issues due to not fully pulling update commits

# 0.7.0

## Added

- Portal occlusion culling (thanks to Zaggy1024!)

# 0.6.2

## Fixed

- Fallback tile being used when none is available

# 0.6.1

## Removed

- Lights no longer gets removed from volumetric lighting

# 0.6.0

## Added

- Remembers player's last valid position when out of bounds
- Culling for radar boosters

# 0.5.3

## Fixed

- Culling distance options works the other way around

# 0.5.2

## Fixed

- Culling finishes early after the first target

# 0.5.1

## Added

- Control for far plane distance when player is on the surface
- Volumetric lighting is disabled in factories

## Changed

- Far plane clips further when player is outside

## Fixed

- Culling stops working when any monitored player is outside

# 0.5.0

## Known issues

- Culling also happens when you're outside

## Changed

- Distance-based culling now modifies the camera's far plane, allowing parts of a tile to be culled
- Depth-based culling now applies to all players

# 0.4.1

## Added

- Changelog

# 0.4.0

## Changed

- Update frequency now affects all culling methods
- Moved logging option to the **general** category

## Fixed

- Tiles with null doorways halting the culling process

# 0.3.0

## Added

- All meshes will be culled when the factory is not being observed*

*Note: The local player (yourself), and all monitors are all outside the factory.

(Read the full commit history [here](https://github.com/2394425147/LC_CullFactory/commits/master/))
