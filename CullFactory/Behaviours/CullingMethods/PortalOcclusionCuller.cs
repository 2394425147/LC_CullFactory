using System.Collections.Generic;
using System.Linq;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private readonly Plane[] _withinTileTestingPlanes = new Plane[3];

    protected override void AddVisibleObjects(List<Camera> cameras, List<TileContents> visibleTiles, List<GrabbableObjectContents> visibleItems, List<Light> visibleLights)
    {
        foreach (var camera in cameras)
        {
            if (camera.orthographic)
            {
                AddAllObjectsWithinOrthographicCamera(camera, visibleTiles, visibleItems, visibleLights);
                continue;
            }

            var currentTileContents = camera.transform.position.GetTileContents();

            if (currentTileContents != null)
            {
                VisibilityTesting.CallForEachLineOfSight(camera, currentTileContents.tile, (tiles, frustums, index) =>
                {
                    var tile = DungeonCullingInfo.TileContentsForTile[tiles[index]];
                    visibleTiles.Add(tile);

                    foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
                    {
                        if (visibleItems.Contains(itemContents))
                            continue;
                        if (!itemContents.IsWithin(tile.bounds))
                            continue;
                        visibleItems.Add(itemContents);
                    }
                });
            }
            else
            {
                visibleItems.AddRange(DynamicObjects.AllGrabbableObjectContentsOutside);
                visibleLights.AddRange(DynamicObjects.AllLightsOutside);
            }
        }

        // Make any objects that should occlude light shining into the directly visible tiles also visible.
        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
        {
            foreach (var visibleTile in visibleTiles)
            {
                foreach (var externalLightLineOfSight in visibleTile.externalLightLinesOfSight)
                {
                    if (visibleItems.Contains(itemContents))
                        continue;
                    if (!itemContents.IsVisible(externalLightLineOfSight))
                        continue;

                    visibleItems.Add(itemContents);
                }
            }
        }

        // Make tiles visible that occlude light from any dynamic lights that shine into the directly visible tiles.
        foreach (var dynamicLight in DynamicObjects.AllLightsInInterior)
        {
            if (dynamicLight == null)
                continue;
            if (visibleLights.Contains(dynamicLight))
                continue;
            if (!dynamicLight.isActiveAndEnabled)
                continue;
            if (!dynamicLight.Affects(visibleTiles))
                continue;

            bool lightPassesThroughOccluders = dynamicLight.PassesThroughOccluders();
            if (lightPassesThroughOccluders)
                visibleLights.Add(dynamicLight);
            if (!dynamicLight.HasShadows())
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

                tiles[lastIndex].Bounds.GetFarthestPlanesNonAlloc(dynamicLightPosition, _withinTileTestingPlanes);

                foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
                {
                    if (visibleItems.Contains(itemContents))
                        continue;
                    if (!itemContents.IsVisible(frustums, lastIndex))
                        continue;
                    if (!itemContents.IsVisible(_withinTileTestingPlanes))
                        continue;

                    visibleItems.Add(itemContents);
                }

                if (!lightPassesThroughOccluders)
                    visibleLights.Add(dynamicLight);
            });
        }
    }
}
