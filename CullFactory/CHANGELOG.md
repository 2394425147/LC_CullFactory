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
