using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using CullFactory.Extenders;
using DunGen;
using UnityEngine;

namespace CullFactory.Behaviours;

/// <summary>
/// DynamicCuller instances are tied to each moon
/// </summary>
public sealed class DynamicCuller : MonoBehaviour
{
    private static float CullDistance    => Plugin.Configuration.CullDistance.Value;
    private static float SqrCullDistance => CullDistance * CullDistance;

    private static readonly List<ManualCameraRenderer>                 Monitors              = new();
    private static readonly ConcurrentDictionary<Tile, TileVisibility> VisibleTilesThisFrame = new();
    private static readonly Queue<TileDepthTester>                     DepthTesterQueue      = new();

    private static List<ManualCameraRenderer> _enabledMonitors = new();

    private static Vector3 _playerPosition;
    private static float   _lastUpdateTime;
    private static bool    _depthTested;

    private void OnEnable()
    {
        Monitors.Clear();

        foreach (var cameraRenderer in FindObjectsByType<ManualCameraRenderer>(FindObjectsSortMode.None))
        {
            var isMonitorCamera = cameraRenderer.mapCamera != null;

            if (!isMonitorCamera)
                continue;

            Monitors.Add(cameraRenderer);
        }
    }

    public void Update()
    {
        if (StartOfRound.Instance.allPlayersDead)
            return;

        if (Time.time - _lastUpdateTime < 1 / Plugin.Configuration.UpdateFrequency.Value)
            return;

        _lastUpdateTime = Time.time;

        var localPlayer = GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                              ? GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript
                              : GameNetworkManager.Instance.localPlayerController;

        _playerPosition  = localPlayer.transform.position;
        _enabledMonitors = Monitors.FindAll(monitor => monitor.mapCamera.enabled);

        var isAnyPlayerInsideFactory = localPlayer.isInsideFactory ||
                                       _enabledMonitors.FindIndex(monitor => monitor.targetedPlayer.isInsideFactory) != -1;

        if (isAnyPlayerInsideFactory)
            EstablishVisibleTiles();

        foreach (var container in LevelGenerationExtender.MeshContainers.Values)
            container.SetVisible(VisibleTilesThisFrame.ContainsKey(container.parentTile));
    }

    private static void EstablishVisibleTiles()
    {
        VisibleTilesThisFrame.Clear();

        if (Plugin.Configuration.UseAdjacentRoomTesting.Value)
        {
            foreach (var tile in LevelGenerationExtender.MeshContainers.Keys)
            {
                if (!tile.Bounds.Contains(_playerPosition))
                    continue;

                OccludeByTileBranching(tile);
                break;
            }
        }

        _depthTested = VisibleTilesThisFrame.Count != 0;

        if (Plugin.Configuration.UseMultithreading.Value)
            OccludeByDistanceParallel();
        else
            OccludeByDistance();
    }

    private static void OccludeByTileBranching(Tile origin)
    {
        DepthTesterQueue.Clear();
        DepthTesterQueue.Enqueue(new TileDepthTester(origin, 0));

        while (DepthTesterQueue.Count > 0)
        {
            var tile = DepthTesterQueue.Dequeue();

            VisibleTilesThisFrame.TryAdd(tile.tile, LevelGenerationExtender.MeshContainers[tile.tile]);

            if (tile.iteration == Plugin.Configuration.MaxBranchingDepth.Value - 1)
                continue;

            foreach (var doorway in tile.tile.UsedDoorways)
            {
                var neighborTile = doorway.ConnectedDoorway.Tile;

                // This is possible due to traversing to a tile that has null doorways
                if (VisibleTilesThisFrame.ContainsKey(neighborTile) ||
                    !LevelGenerationExtender.MeshContainers.ContainsKey(neighborTile))
                    continue;

                DepthTesterQueue.Enqueue(new TileDepthTester(neighborTile, tile.iteration + 1));
            }
        }
    }

    private static void OccludeByDistanceParallel()
    {
        Parallel.ForEach(LevelGenerationExtender.MeshContainers.Values, TestVisibility);
    }

    private static void OccludeByDistance()
    {
        foreach (var container in LevelGenerationExtender.MeshContainers.Values)
            TestVisibility(container);
    }

    private static void TestVisibility(TileVisibility container)
    {
        if (VisibleTilesThisFrame.ContainsKey(container.parentTile)) return;

        var position = container.parentTile.transform.position;

        var shouldBeVisible = false;

        if (!_depthTested)
            shouldBeVisible = Vector3.SqrMagnitude(position - _playerPosition) <= SqrCullDistance;

        foreach (var monitor in _enabledMonitors)
        {
            if (shouldBeVisible)
                break;

            shouldBeVisible |= Vector3.SqrMagnitude(position - monitor.targetedPlayer.transform.position) <= SqrCullDistance;
        }

        if (shouldBeVisible)
            VisibleTilesThisFrame.TryAdd(container.parentTile, container);
    }
}

internal struct TileDepthTester
{
    public readonly Tile tile;
    public readonly int  iteration;

    public TileDepthTester(Tile tile, int iteration)
    {
        this.tile      = tile;
        this.iteration = iteration;
    }
}
