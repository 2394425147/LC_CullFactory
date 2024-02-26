using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    protected override void AddVisibleTiles(List<TileContents> visibleTiles)
    {
        foreach (var camera in Camera.allCameras)
        {
            if ((camera.cullingMask & DungeonCullingInfo.AllTileLayersMask) == 0)
                continue;
            visibleTiles.AddContentsVisibleToCamera(camera);
        }
    }
}
