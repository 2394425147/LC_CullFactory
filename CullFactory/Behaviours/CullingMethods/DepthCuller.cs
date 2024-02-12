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
    private readonly List<TileContents> _visibleTilesThisFrame = [];

    private float _lastUpdateTime;

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);

        _visibleTilesThisFrame.Clear();
    }

    public void LateUpdate()
    {
        if (StartOfRound.Instance.allPlayersDead ||
            Time.time - _lastUpdateTime < 1 / Plugin.Configuration.UpdateFrequency.Value)
            return;

        _lastUpdateTime = Time.time;

        _visibleTilesThisFrame.SetVisible(false);
        _visibleTilesThisFrame.Clear();

        foreach (var camera in Camera.allCameras)
        {
            if (camera.orthographic)
            {
                DungeonCullingInfo.CollectAllTilesWithinCameraFrustum(camera, _visibleTilesThisFrame);
                continue;
            }

            var cameraTile = camera.transform.position.GetTileContents();
            if (cameraTile == null)
                continue;
            IncludeNearbyTiles(cameraTile.tile);
        }

        _visibleTilesThisFrame.SetVisible(true);
    }

    private void IncludeNearbyTiles(Tile origin)
    {
        var depthTarget = Plugin.Configuration.MaxBranchingDepth.Value - 1;
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
            _visibleTilesThisFrame.Add(DungeonCullingInfo.TileContentsForTile[tileFrame.tile]);
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

    private void OnDestroy()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
    }
}

internal struct TileDepthTester
{
    public readonly Tile tile;
    public readonly int iteration;

    public TileDepthTester(Tile tile, int iteration)
    {
        this.tile = tile;
        this.iteration = iteration;
    }
}