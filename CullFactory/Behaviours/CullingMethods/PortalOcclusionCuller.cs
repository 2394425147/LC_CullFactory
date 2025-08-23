using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private float _camerasTime = 0;
    private float _visibilityTime = 0;
    private float _itemBoundsTime = 0;
    private float _itemShadowsTime = 0;
    private float _dynamicLightsLineOfSightTime = 0;
    private float _dynamicLightsTime = 0;

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
        var camerasStart = GetProfileTime();

        var interiorIsVisible = false;
        var exteriorIsVisible = false;

        var getCameraPositionTime = 0f;
        var visibilityTime = 0f;

        foreach (var camera in cameras)
        {
            if (camera.orthographic)
            {
                AddAllObjectsWithinOrthographicCamera(camera, visibility);
                continue;
            }

            var getCameraPositionStart = GetProfileTime();
            var cameraPosition = camera.transform.position;
            getCameraPositionTime += GetProfileTime() - getCameraPositionStart;
            var cameraTile = cameraPosition.GetTileContents();

            if (cameraTile != null)
            {
                interiorIsVisible = true;
                _debugTile ??= cameraTile;

                var visibilityStart = GetProfileTime();
                VisibilityTesting.CallForEachLineOfSight(camera, cameraTile, (tiles, frustums, index) =>
                {
                    visibility.directTiles.Add(tiles[index]);
                });
                visibilityTime += GetProfileTime() - visibilityStart;
            }
            else if (!exteriorIsVisible)
            {
                visibility.items.UnionWith(DynamicObjects.AllGrabbableObjectContentsOutside);
                visibility.dynamicLights.UnionWith(DynamicObjects.AllLightsOutside);
                exteriorIsVisible = true;
            }
        }

        _camerasTime += GetProfileTime() - camerasStart - getCameraPositionTime;

        if (!interiorIsVisible)
            return;

        var dynamicLightsStart = GetProfileTime();
        var dynamicLightsLineOfSightTime = 0f;

        // Make tiles visible that occlude light from any dynamic lights that shine into the directly visible tiles.
        foreach (var dynamicLight in DynamicObjects.AllLightsInInterior)
        {
            if (dynamicLight == null)
                continue;
            if (!dynamicLight.isActiveAndEnabled)
            {
                visibility.dynamicLights.Add(dynamicLight);
                continue;
            }
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

            var dynamicLightsLineOfSightStart = GetProfileTime();
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

            dynamicLightsLineOfSightTime += GetProfileTime() - dynamicLightsLineOfSightStart;
        }

        var dynamicLightsTime = GetProfileTime() - dynamicLightsStart;

        var itemBoundsTime = 0f;
        var itemShadowsStart = GetProfileTime();

        // Make any objects that are directly visible or should occlude light shining into the directly visible tiles visible.
        foreach (var itemContents in DynamicObjects.AllGrabbableObjectContentsInInterior)
        {
            var itemBoundsStart = GetProfileTime();
            itemContents.CalculateBounds();
            itemBoundsTime += GetProfileTime() - itemBoundsStart;

            if (ItemIsVisible(itemContents, visibility))
                visibility.items.Add(itemContents);
        }

        var itemShadowsTime = GetProfileTime() - itemShadowsStart;

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
