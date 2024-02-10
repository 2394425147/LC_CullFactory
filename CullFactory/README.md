![Banner](https://github.com/2394425147/LC_CullFactory/blob/master/CullFactory/Documentation/banner.png)

**Note:** Performance gains may vary between devices. Feel free to give it a try and experiment with different options!

## Overview

- Stops objects that are not visible or too far away from being rendered.
- Designed to support radar map monitors.

## Culling Methods

### Portal Occlusion Culling
This method is intended to hide all objects that are not visible to a camera. It does so by recursively checking which other tiles are visible to the camera through doorways and connecting pathways between tiles in the interior.

**Note for developers:** The `Doorway.Socket.Size` field is used to determine the bounds of every doorway in the interior. If the size does not encompass the entirety of the possible visible portions of the next tile, then neighboring tiles will likely pop out of visibility when they shouldn't.

### Depth Culling
This is a more naive method that will make tiles visible that are separated from the camera by a certain number of tiles. This may result in hallways becoming invisible in view of the camera.
