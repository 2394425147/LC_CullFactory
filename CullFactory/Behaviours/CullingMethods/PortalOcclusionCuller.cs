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

    private readonly HashSet<TileContents> _dynamicLightOccludingTiles = [];

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

    private static bool ItemIsVisible(GrabbableObjectContents item, VisibilitySets visibility)
    {
        foreach (var tile in visibility.directTiles)
        {
            if (item.IsWithin(tile))
                return true;

            foreach (var externalLightLineOfSight in tile.externalLightLinesOfSight)
            {
                if (item.IsVisible(externalLightLineOfSight))
                    return true;
            }
        }

        foreach (var tile in visibility.indirectTiles)
        {
            if (item.IsWithin(tile))
                return true;
        }

        return false;
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
                _debugTile ??= cameraTile;

                var visibilityStart = Time.realtimeSinceStartupAsDouble;
                VisibilityTesting.CallForEachLineOfSight(camera, cameraTile, (tiles, frustums, index) =>
                {
                    visibility.directTiles.Add(tiles[index]);
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

        if (_benchmarking)
            _camerasTime += (Time.realtimeSinceStartupAsDouble - camerasStart) - getCameraPositionTime;

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
            if (!dynamicLight.Affects(visibility.directTiles))
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
            _dynamicLightOccludingTiles.Clear();
            var reachesAVisibleTile =
                VisibilityTesting.CallForEachLineOfSightTowardTiles(dynamicLightPosition, lightTileContents, visibility.directTiles, (tiles, frustums, lastIndex) =>
                {
                    var tile = tiles[lastIndex];
                    if (!visibility.directTiles.Contains(tile))
                        _dynamicLightOccludingTiles.Add(tile);
                });

            if (!lightPassesThroughOccluders && reachesAVisibleTile)
            {
                visibility.indirectTiles.UnionWith(_dynamicLightOccludingTiles);
                visibility.dynamicLights.Add(dynamicLight);
            }

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

            if (ItemIsVisible(itemContents, visibility))
                visibility.items.Add(itemContents);
        }

        var itemShadowsTime = Time.realtimeSinceStartupAsDouble - itemShadowsStart;

        if (_benchmarking)
        {
            _visibilityTime += visibilityTime;
            _itemBoundsTime += itemBoundsTime;
            _dynamicLightsLineOfSightTime += dynamicLightsLineOfSightTime;
            _dynamicLightsTime += dynamicLightsTime;
            _itemShadowsTime += itemShadowsTime;
        }
    }
}
