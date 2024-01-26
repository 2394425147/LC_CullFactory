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

    private static List<ManualCameraRenderer> _enabledMonitors = new();

    private static Vector3 _playerPosition;
    private static Tile    _lastFoundPlayerTile;
    private static float   _depthCullingUpdateTime;

    private void OnEnable()
    {
        if (Monitors.Count != 0)
            return;

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

        VisibleTilesThisFrame.Clear();

        var localPlayer = GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                              ? GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript
                              : GameNetworkManager.Instance.localPlayerController;

        _playerPosition = localPlayer.transform.position;

        if (Plugin.Configuration.UseAdjacentRoomTesting.Value &&
            Time.time - _depthCullingUpdateTime > 1 / Plugin.Configuration.AdjacentRoomUpdateFrequency.Value)
        {
            _depthCullingUpdateTime = Time.time;

            _lastFoundPlayerTile = null;
            foreach (var tile in LevelGenerationExtender.MeshContainers.Keys)
            {
                if (!tile.Bounds.Contains(_playerPosition))
                    continue;

                _lastFoundPlayerTile = tile;
                break;
            }
        }

        if (_lastFoundPlayerTile != null)
            OccludeByTileBranching(_lastFoundPlayerTile);

        _enabledMonitors = Monitors.FindAll(monitor => monitor.mapCamera.enabled);

        if (Plugin.Configuration.UseMultithreading.Value)
            OccludeByDistanceParallel();
        else
            OccludeByDistance();

        foreach (var container in LevelGenerationExtender.MeshContainers.Values)
            container.SetVisible(VisibleTilesThisFrame.ContainsKey(container.parentTile));
    }

    private static void OccludeByTileBranching(Tile origin)
    {
        var queue = new Queue<TileDepthTester>();

        queue.Enqueue(new TileDepthTester(origin, 0));

        while (queue.Count > 0)
        {
            var tile = queue.Dequeue();

            if (tile.iteration == Plugin.Configuration.MaxBranchingDepth.Value)
                break;

            if (VisibleTilesThisFrame.ContainsKey(tile.tile))
                continue;

            VisibleTilesThisFrame.TryAdd(tile.tile, LevelGenerationExtender.MeshContainers[tile.tile]);

            foreach (var child in tile.tile.UsedDoorways)
                queue.Enqueue(new TileDepthTester(child.ConnectedDoorway.Tile, tile.iteration + 1));
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

        if (_lastFoundPlayerTile == null)
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
