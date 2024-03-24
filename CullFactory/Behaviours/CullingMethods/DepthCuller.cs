using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using DunGen;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

/// <summary>
///     DepthCuller instances are tied to each moon
/// </summary>
public sealed class DepthCuller : CullingMethod
{
    protected override void AddVisibleObjects(List<Camera> cameras, VisibilitySets visibility)
    {
        foreach (var camera in cameras)
        {
            if (camera.orthographic)
            {
                AddAllObjectsWithinOrthographicCamera(camera, visibility);
                continue;
            }

            var cameraTile = camera.transform.position.GetTileContents();
            if (cameraTile == null)
            {
                visibility.items.UnionWith(DynamicObjects.AllGrabbableObjectContentsOutside);
                visibility.dynamicLights.UnionWith(DynamicObjects.AllLightsOutside);
                continue;
            }
            visibility.debugTile ??= cameraTile;
            IncludeNearbyTiles(cameraTile.tile, visibility.tiles);

            foreach (var item in DynamicObjects.AllGrabbableObjectContentsInInterior)
            {
                if (item.IsWithin(visibility.tiles))
                    visibility.items.Add(item);
            }
            foreach (var light in DynamicObjects.AllLightsInInterior)
            {
                if (light.Affects(visibility.tiles))
                    visibility.dynamicLights.Add(light);
            }
        }
    }

    private void IncludeNearbyTiles(Tile origin, HashSet<TileContents> visibleTiles)
    {
        var depthTarget = Config.MaxBranchingDepth.Value - 1;
        // Guess that there will be 2 used doors per tile on average. Maybe a bit excessive.
        var depthTesterQueue = new Stack<TileDepthTester>(depthTarget * depthTarget);
        var traversedTiles = new HashSet<Tile>();

        depthTesterQueue.Clear();
        depthTesterQueue.Push(new TileDepthTester(origin, 0));

        while (depthTesterQueue.Count > 0)
        {
            var tileFrame = depthTesterQueue.Pop();

            // We use a second list here so other depth searches won't interfere
            // E.g. A is the first process, B is the second:
            //       [B3]  <<- Not traversed because B sees (A2) as done and will halt early
            // [A1]  [A2]  [A3]
            //       [B1]
            visibleTiles.Add(DungeonCullingInfo.TileContentsForTile[tileFrame.tile]);
            traversedTiles.Add(tileFrame.tile);

            if (tileFrame.iteration == depthTarget)
                continue;

            foreach (var doorway in tileFrame.tile.UsedDoorways)
            {
                var neighborTile = doorway.ConnectedDoorway.Tile;

                if (traversedTiles.Contains(neighborTile))
                    continue;

                depthTesterQueue.Push(new TileDepthTester(neighborTile, tileFrame.iteration + 1));
            }
        }
    }
}

internal readonly struct TileDepthTester(Tile tile, int iteration)
{
    public readonly Tile tile = tile;
    public readonly int iteration = iteration;
}