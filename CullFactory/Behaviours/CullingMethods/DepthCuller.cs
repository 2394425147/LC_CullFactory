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
    private readonly HashSet<TileContents> _visibleTiles = [];

    private float _lastUpdateTime;

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);
        _visibleTiles.Clear();
    }

    public void LateUpdate()
    {
        if (StartOfRound.Instance.allPlayersDead ||
            Time.time - _lastUpdateTime < 1 / Config.UpdateFrequency.Value)
            return;

        _lastUpdateTime = Time.time;

        _visibleTiles.Clear();

        foreach (var camera in Camera.allCameras)
        {
            if (camera.orthographic)
            {
                _visibleTiles.FindFromCameraOrthographic(camera);
                continue;
            }

            var cameraTile = camera.transform.position.GetTileContents();
            if (cameraTile == null)
                continue;
            IncludeNearbyTiles(cameraTile.tile);
        }

        foreach (var tileContents in DungeonCullingInfo.AllTileContents)
            tileContents.SetVisible(_visibleTiles.Contains(tileContents));
    }

    private void IncludeNearbyTiles(Tile origin)
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
            _visibleTiles.Add(DungeonCullingInfo.TileContentsForTile[tileFrame.tile]);
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

internal struct TileDepthTester(Tile tile, int iteration)
{
    public readonly Tile tile = tile;
    public readonly int iteration = iteration;
}
