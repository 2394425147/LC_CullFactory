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

### Dungeons

The `Doorway.Socket.Size` field is used to determine the bounds of every doorway in the interior. If the size does not encompass the entirety of the possible visible portions of any tiles that can connect to the socket, then those tiles may visibly disappear.

To determine which tile the camera is within, the tiles are searched in the order they were created until a tile with bounds intersecting the camera is found. If the camera is not within any tile bounds, then the closest tile within a certain radius of the camera is used instead. This radius is very lenient, but please try to ensure that your tile bounds are accurate, as inaccurate tile bounds can also lead to players getting stuck in walls.

### Items

Item/dynamic light culling places each `GrabbableObject` into one of two pools, interior or exterior. To avoid unnecessary work, items are only moved between pools when they are:

- Held by a teleporting player
- Held by a teleporting enemy
- Spawned
- Dropped
- Shown or hidden, i.e. when changing inventory slots
- Grabbed by an enemy

If you are teleporting any item in or out of the dungeon and it is not covered by the above cases, a call to `GrabbableObject.EnableItemMeshes()` will cause CullFactory to move the item to the appropriate pool before the current frame is rendered.
