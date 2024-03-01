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

        foreach (var dynamicLight in DynamicObjects.AllLightsInInterior)
        {
            if (dynamicLight == null)
                continue;
            if (!dynamicLight.isActiveAndEnabled)
                continue;
            if (!dynamicLight.Affects(visibleTiles))
                continue;

            var dynamicLightPosition = dynamicLight.transform.position;
            var lightTileContents = dynamicLightPosition.GetTileContents();
            if (lightTileContents == null)
                continue;

            VisibilityTesting.CallForEachLineOfSightToTiles(dynamicLightPosition, lightTileContents.tile, visibleTiles, (tiles, frustums, lastIndex) =>
            {
                for (var i = 0; i < lastIndex; i++)
                {
                    var tileContents = DungeonCullingInfo.TileContentsForTile[tiles[i]];
                    if (!visibleTiles.Contains(tileContents))
                        visibleTiles.Add(tileContents);
                }
            });
        }
    }
}
