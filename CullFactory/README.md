![Banner](https://raw.githubusercontent.com/2394425147/LC_CullFactory/master/CullFactory/Documentation/banner.png)

**Note:** Performance gains may vary between devices. Feel free to give it a try and experiment with different options!

## Overview

- Stops objects that are not visible, or are too far from the camera, from being rendered.
    - Interior rooms, items, and lights will be culled.
    - The default settings are designed to allow zero visual artifacts or popping.
- Compatible with any mods that add cameras.

## Culling Methods

### Portal Occlusion Culling

The default culling method, this is intended to hide all objects that are not visible to a camera without affecting visuals. It does so by recursively checking which other tiles are visible to the camera through doorways and connecting pathways between tiles in the interior.

### Depth Culling

This is a more naive method that will make tiles visible that are separated from the camera by a certain number of tiles. It may result in hallways becoming invisible in view of the camera.

## Developer Information

### Rooms are invisible in my interior!

**Neighboring rooms are invisible:** The `Doorway.Socket.Size` field is used to determine the bounds of every doorway in the interior. If the size does not encompass the entirety of the possible visible portions of any tiles that can connect to the socket, then those tiles may visibly disappear.

**The player's room is invisible:** To determine which tile the camera is within, the tiles are searched in the order they were created until a tile with bounds intersecting the camera is found. If the camera is not within any tile bounds, then the closest tile within a certain radius of the camera is used instead. This radius is very lenient, but please try to ensure that your tile bounds are accurate, as inaccurate tile bounds can also lead to players getting stuck in walls.

**Important note:** DunGen gets very confused about the bounds of a `Tile` when the object it resides on is scaled. It is best to always keep the scale of your tile objects set to identity (1, 1, 1). If your models need to be scaled, place them into a child object and scale that rather than the tile.

### Items are invisible after I move them in or out of the interior!

If items are invisible after your code has teleported them in or out of the interior, you may want to call `GrabbableObject.EnableItemMeshes()` to fix the issue.

Item/dynamic light culling places each `GrabbableObject` into one of two pools, interior or exterior. If an item is in a pool that is not currently visible to any cameras, it will be invisible. To avoid unnecessary work, items are only moved between pools when they are:

- Spawned
- Dropped
- Shown or hidden, i.e. when changing inventory slots
- Grabbed by an enemy
- Held by a player teleporting via an `EntranceTeleport` or `ShipTeleporter`
- Held by an enemy whose state was changed via `EnemyAI.SetEnemyOutside()`

If you are teleporting any item in or out of the dungeon and it is not covered by the above cases, a call to `GrabbableObject.EnableItemMeshes()` will cause CullFactory to move the item to the appropriate pool before the current frame is rendered.
