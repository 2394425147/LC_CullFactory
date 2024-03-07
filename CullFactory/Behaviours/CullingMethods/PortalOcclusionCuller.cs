using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private readonly Plane[] _withinTileTestingPlanes = new Plane[3];

    protected override void AddVisibleObjects(List<Camera> cameras, List<TileContents> visibleTiles, List<GrabbableObjectContents> visibleItems, List<Light> visibleLights)
    {
        var camerasStart = Time.realtimeSinceStartupAsDouble;

        var interiorIsVisible = false;

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
                interiorIsVisible = true;

                VisibilityTesting.CallForEachLineOfSight(camera, currentTileContents, (tiles, frustums, index) =>
                {
                    visibleTiles.Add(tiles[index]);
                });
            }
            else
            {
                visibleItems.AddRange(DynamicObjects.AllGrabbableObjectContentsOutside);
                visibleLights.AddRange(DynamicObjects.AllLightsOutside);
            }
        }

        var camerasTime = Time.realtimeSinceStartupAsDouble - camerasStart;

        if (!interiorIsVisible)
            return;

        var itemBoundsTime = 0d;
        var itemShadowsStart = Time.realtimeSinceStartupAsDouble;

        // Make any objects that are directly visible or should occlude light shining into the directly visible tiles visible.
        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
        {
            var itemBoundsStart = Time.realtimeSinceStartupAsDouble;
            itemContents.CalculateBounds();
            itemBoundsTime += Time.realtimeSinceStartupAsDouble - itemBoundsStart;

            var visibleTileCount = visibleTiles.Count;
            for (var i = 0; i < visibleTileCount; i++)
            {
                var visibleTile = visibleTiles[i];

                if (itemContents.IsWithin(visibleTile.bounds))
                {
                    visibleItems.Add(itemContents);
                    continue;
                }

                foreach (var externalLightLineOfSight in visibleTile.externalLightLinesOfSight)
                {
                    if (itemContents.IsVisible(externalLightLineOfSight))
                    {
                        visibleItems.Add(itemContents);
                        i = visibleTileCount;
                        break;
                    }
                }
            }
        }

        var itemShadowsTime = Time.realtimeSinceStartupAsDouble - itemShadowsStart;

        var dynamicLightsStart = Time.realtimeSinceStartupAsDouble;
        var dynamicLightsLineOfSightTime = 0d;

        // Make tiles visible that occlude light from any dynamic lights that shine into the directly visible tiles.
        foreach (var dynamicLight in DynamicObjects.AllLightsInInterior)
        {
            if (dynamicLight == null)
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

            var dynamicLightsLineOfSightStart = Time.realtimeSinceStartupAsDouble;
            VisibilityTesting.CallForEachLineOfSightToTiles(dynamicLightPosition, lightTileContents, visibleTiles, (tiles, frustums, lastIndex) =>
            {
                for (var i = 0; i < lastIndex; i++)
                {
                    var tileContents = tiles[i];
                    if (!visibleTiles.Contains(tileContents))
                        visibleTiles.Add(tileContents);
                }

                tiles[lastIndex].bounds.GetFarthestPlanesNonAlloc(dynamicLightPosition, _withinTileTestingPlanes);

                foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
                {
                    if (!itemContents.IsVisible(frustums, lastIndex))
                        continue;
                    if (!itemContents.IsVisible(_withinTileTestingPlanes))
                        continue;

                    visibleItems.Add(itemContents);
                }

                if (!lightPassesThroughOccluders)
                    visibleLights.Add(dynamicLight);
            });
            dynamicLightsLineOfSightTime += Time.realtimeSinceStartupAsDouble - dynamicLightsLineOfSightStart;
        }

        var dynamicLightsTime = Time.realtimeSinceStartupAsDouble - dynamicLightsStart;

        var totalTime = camerasTime + itemShadowsTime + dynamicLightsTime;
        Plugin.Log($"Total time {totalTime * 1000000} microseconds, cameras took {camerasTime * 1000000} microseconds, calculating item bounds took {itemBoundsTime * 1000000} microseconds, item shadows took {(itemShadowsTime - itemBoundsTime) * 1000000} microseconds, dynamic lights line of sight checks took {dynamicLightsLineOfSightTime * 1000000} microseconds, dynamic lights took {(dynamicLightsTime - dynamicLightsLineOfSightTime) * 1000000} microseconds.");
    }
}
