using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private double _camerasTime = 0;
    private double _visibilityTime = 0;
    private double _itemBoundsTime = 0;
    private double _itemShadowsTime = 0;
    private double _dynamicLightsLineOfSightTime = 0;
    private double _dynamicLightsTime = 0;

    protected override void BenchmarkEnded()
    {
        base.BenchmarkEnded();

        var avgCamerasTime = _camerasTime / _totalCalls;
        var avgVisibilityTime = _visibilityTime / _totalCalls;
        var avgItemBoundsTime = _itemBoundsTime / _totalCalls;
        var avgItemShadowsTime = _itemShadowsTime / _totalCalls;
        var avgDynamicLightsLineOfSightTime = _dynamicLightsLineOfSightTime / _totalCalls;
        var avgDynamicLightsTime = _dynamicLightsTime / _totalCalls;
        var avgTotalTime = avgCamerasTime + avgItemShadowsTime + avgDynamicLightsTime;
        Plugin.Log($"Total portal occlusion culling time {avgTotalTime * 1000000:0.####} microseconds.\n" +
            $"    Cameras took {avgCamerasTime * 1000000:0.####} microseconds.\n" +
            $"    Direct visibility testing took {avgVisibilityTime * 1000000:0.####} microseconds.\n" +
            $"    Calculating item bounds took {avgItemBoundsTime * 1000000:0.####} microseconds.\n" +
            $"    Dynamic lights line of sight checks took {avgDynamicLightsLineOfSightTime * 1000000:0.####} microseconds.\n" +
            $"    Dynamic light influence checks took {(avgDynamicLightsTime - avgDynamicLightsLineOfSightTime) * 1000000:0.####} microseconds.\n" +
            $"    Item shadows took {(avgItemShadowsTime - avgItemBoundsTime) * 1000000:0.####} microseconds.");

        _camerasTime = 0;
        _visibilityTime = 0;
        _itemBoundsTime = 0;
        _itemShadowsTime = 0;
        _dynamicLightsLineOfSightTime = 0;
        _dynamicLightsTime = 0;
        _totalCalls = 0;
    }

    protected override void AddVisibleObjects(List<Camera> cameras, VisibilitySets visibility)
    {
        var camerasStart = Time.realtimeSinceStartupAsDouble;

        var interiorIsVisible = false;
        var exteriorIsVisible = false;

        var getCameraPositionTime = 0d;
        var visibilityTime = 0d;

        foreach (var camera in cameras)
        {
            if (camera.orthographic)
            {
                AddAllObjectsWithinOrthographicCamera(camera, visibility);
                continue;
            }

            var getCameraPositionStart = Time.realtimeSinceStartupAsDouble;
            var cameraPosition = camera.transform.position;
            getCameraPositionTime += Time.realtimeSinceStartupAsDouble - getCameraPositionStart;
            var cameraTile = cameraPosition.GetTileContents();

            if (cameraTile != null)
            {
                interiorIsVisible = true;
                visibility.debugTile ??= cameraTile;

                var visibilityStart = Time.realtimeSinceStartupAsDouble;
                VisibilityTesting.CallForEachLineOfSight(camera, cameraTile, (tiles, frustums, index) =>
                {
                    visibility.tiles.Add(tiles[index]);
                });
                visibilityTime += Time.realtimeSinceStartupAsDouble - visibilityStart;
            }
            else if (!exteriorIsVisible)
            {
                visibility.items.UnionWith(DynamicObjects.AllGrabbableObjectContentsOutside);
                visibility.dynamicLights.UnionWith(DynamicObjects.AllLightsOutside);
                exteriorIsVisible = true;
            }
        }

        var camerasTime = Time.realtimeSinceStartupAsDouble - camerasStart;

        if (!interiorIsVisible)
            return;

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
                    visibility.tiles.Add(tiles[i]);

                if (!lightPassesThroughOccluders)
                    visibility.dynamicLights.Add(dynamicLight);
            });
            dynamicLightsLineOfSightTime += Time.realtimeSinceStartupAsDouble - dynamicLightsLineOfSightStart;
        }

        var dynamicLightsTime = Time.realtimeSinceStartupAsDouble - dynamicLightsStart;

        var itemBoundsTime = 0d;
        var itemShadowsStart = Time.realtimeSinceStartupAsDouble;

        // Make any objects that are directly visible or should occlude light shining into the directly visible tiles visible.
        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
        {
            var itemBoundsStart = Time.realtimeSinceStartupAsDouble;
            itemContents.CalculateBounds();
            itemBoundsTime += Time.realtimeSinceStartupAsDouble - itemBoundsStart;

            foreach (var visibleTile in visibility.tiles)
            {
                if (itemContents.IsWithin(visibleTile.bounds))
                {
                    visibility.items.Add(itemContents);
                    continue;
                }

                bool addedItem = false;
                foreach (var externalLightLineOfSight in visibleTile.externalLightLinesOfSight)
                {
                    if (itemContents.IsVisible(externalLightLineOfSight))
                    {
                        visibility.items.Add(itemContents);
                        addedItem = true;
                        break;
                    }
                }

                if (addedItem)
                    break;
            }
        }

        var itemShadowsTime = Time.realtimeSinceStartupAsDouble - itemShadowsStart;

        if (_benchmarking)
        {
            _camerasTime += camerasTime - getCameraPositionTime;
            _visibilityTime += visibilityTime;
            _itemBoundsTime += itemBoundsTime;
            _dynamicLightsLineOfSightTime += dynamicLightsLineOfSightTime;
            _dynamicLightsTime += dynamicLightsTime;
            _itemShadowsTime += itemShadowsTime;
        }
    }
}
