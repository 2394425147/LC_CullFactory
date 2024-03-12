using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private readonly Plane[] _withinTileTestingPlanes = new Plane[3];

    protected override void AddVisibleObjects(List<Camera> cameras, VisibilitySets visibility)
    {
        var camerasStart = Time.realtimeSinceStartupAsDouble;

        var interiorIsVisible = false;

        foreach (var camera in cameras)
        {
            if (camera.orthographic)
            {
                AddAllObjectsWithinOrthographicCamera(camera, visibility);
                continue;
            }

            var currentTileContents = camera.transform.position.GetTileContents();

            if (currentTileContents != null)
            {
                interiorIsVisible = true;

                VisibilityTesting.CallForEachLineOfSight(camera, currentTileContents, (tiles, frustums, index) =>
                {
                    visibility.tiles.Add(tiles[index]);
                });
            }
            else
            {
                visibility.items.AddRange(DynamicObjects.AllGrabbableObjectContentsOutside);
                visibility.dynamicLights.AddRange(DynamicObjects.AllLightsOutside);
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

            var visibleTileCount = visibility.tiles.Count;
            for (var i = 0; i < visibleTileCount; i++)
            {
                var visibleTile = visibility.tiles[i];

                if (itemContents.IsWithin(visibleTile.bounds))
                {
                    visibility.items.Add(itemContents);
                    continue;
                }

                foreach (var externalLightLineOfSight in visibleTile.externalLightLinesOfSight)
                {
                    if (itemContents.IsVisible(externalLightLineOfSight))
                    {
                        visibility.items.Add(itemContents);
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
            if (!dynamicLight.Affects(visibility.tiles))
                continue;

            bool lightPassesThroughOccluders = dynamicLight.PassesThroughOccluders();
            if (lightPassesThroughOccluders)
                visibility.dynamicLights.Add(dynamicLight);
            if (!dynamicLight.HasShadows())
                continue;

            var dynamicLightPosition = dynamicLight.transform.position;
            var lightTileContents = dynamicLightPosition.GetTileContents();
            if (lightTileContents == null)
                continue;

            var dynamicLightsLineOfSightStart = Time.realtimeSinceStartupAsDouble;
            VisibilityTesting.CallForEachLineOfSightToTiles(dynamicLightPosition, lightTileContents, visibility.tiles, (tiles, frustums, lastIndex) =>
            {
                for (var i = 0; i < lastIndex; i++)
                {
                    var tileContents = tiles[i];
                    if (!visibility.tiles.Contains(tileContents))
                        visibility.tiles.Add(tileContents);
                }

                tiles[lastIndex].bounds.GetFarthestPlanesNonAlloc(dynamicLightPosition, _withinTileTestingPlanes);

                foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
                {
                    if (!itemContents.IsVisible(frustums, lastIndex))
                        continue;
                    if (!itemContents.IsVisible(_withinTileTestingPlanes))
                        continue;

                    visibility.items.Add(itemContents);
                }

                if (!lightPassesThroughOccluders)
                    visibility.dynamicLights.Add(dynamicLight);
            });
            dynamicLightsLineOfSightTime += Time.realtimeSinceStartupAsDouble - dynamicLightsLineOfSightStart;
        }

        var dynamicLightsTime = Time.realtimeSinceStartupAsDouble - dynamicLightsStart;

        var totalTime = camerasTime + itemShadowsTime + dynamicLightsTime;
        Plugin.Log($"Total time {totalTime * 1000000} microseconds, cameras took {camerasTime * 1000000} microseconds, calculating item bounds took {itemBoundsTime * 1000000} microseconds, item shadows took {(itemShadowsTime - itemBoundsTime) * 1000000} microseconds, dynamic lights line of sight checks took {dynamicLightsLineOfSightTime * 1000000} microseconds, dynamic lights took {(dynamicLightsTime - dynamicLightsLineOfSightTime) * 1000000} microseconds.");
    }
}
