# 0.5.1

## Changed

- Far plane clips further when player is outside

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
