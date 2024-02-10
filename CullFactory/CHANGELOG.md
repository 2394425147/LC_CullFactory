# 0.7.2

## Added

- Added options to visualize the portals and tile bounds used to determine culling

## Changed

- Changed the render distance option to be opt-in with the `[Distance culling] Enabled` option

## Fixed

- Partially prevented the entrance room on Scoopy's Sewer interior from appearing invisible
- Fixed depth culling sometimes allowing tiles to remain invisible on cameras
- Fixed issues that could cause the render distance to be set to the interior value when outside or vice versa

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
