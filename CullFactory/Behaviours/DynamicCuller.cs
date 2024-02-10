using System.Collections.Generic;
using CullFactory.Data;
using DunGen;
using UnityEngine;

namespace CullFactory.Behaviours;

/// <summary>
///     DynamicCuller instances are tied to each moon
/// </summary>
public sealed class DynamicCuller : MonoBehaviour
{
    private static readonly List<TileContents> VisibleTilesThisFrame = [];

    private static float _lastUpdateTime;

    private void SetTilesVisible(IEnumerable<TileContents> tiles, bool visible)
    {
        foreach (var tileContents in tiles)
        {
            foreach (var renderer in tileContents.renderers)
                renderer.forceRenderingOff = !visible;
            foreach (var light in tileContents.lights)
                light.enabled = visible;
        }
    }

    private void OnEnable()
    {
        SetTilesVisible(DungeonCullingInfo.AllTileContents, false);
    }

    public void LateUpdate()
    {
        if (StartOfRound.Instance.allPlayersDead ||
            Time.time - _lastUpdateTime < 1 / Plugin.Configuration.UpdateFrequency.Value)
            return;

        _lastUpdateTime = Time.time;

        SetTilesVisible(VisibleTilesThisFrame, false);
        VisibleTilesThisFrame.Clear();

        foreach (var camera in Camera.allCameras)
        {
            if (camera.orthographic)
            {
                DungeonCullingInfo.CollectAllTilesWithinCameraFrustum(camera, VisibleTilesThisFrame);
                continue;
            }

            var cameraTile = camera.transform.position.GetTileContents();
            if (cameraTile == null)
                continue;
            IncludeNearbyTiles(cameraTile.tile);
        }

        SetTilesVisible(VisibleTilesThisFrame, true);
    }

    private static void IncludeNearbyTiles(Tile origin)
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
            VisibleTilesThisFrame.Add(DungeonCullingInfo.TileContentsForTile[tileFrame.tile]);
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
        SetTilesVisible(DungeonCullingInfo.AllTileContents, true);
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