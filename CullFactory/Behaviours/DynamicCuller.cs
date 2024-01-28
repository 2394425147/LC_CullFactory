using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CullFactory.Extenders;
using DunGen;
using UnityEngine;

namespace CullFactory.Behaviours;

/// <summary>
/// DynamicCuller instances are tied to each moon
/// </summary>
public sealed class DynamicCuller : MonoBehaviour
{
    private const float VanillaClipDistance = 400;

    private static readonly ConcurrentDictionary<Tile, TileVisibility> VisibleTilesThisFrame = new();
    private static readonly List<ManualCameraRenderer>                 Monitors              = new();
    private static readonly List<Vector3>                              CullOrigins           = new();

    private static List<ManualCameraRenderer> _enabledMonitors = new();
    private static float                      _lastUpdateTime;

    private void OnEnable()
    {
        Monitors.Clear();

        foreach (var cameraRenderer in FindObjectsByType<ManualCameraRenderer>(FindObjectsSortMode.None))
        {
            var isMonitorCamera = cameraRenderer.mapCamera != null;

            if (!isMonitorCamera)
                continue;

            Monitors.Add(cameraRenderer);

            Plugin.Log($"Found monitor camera \"{cameraRenderer.name}\"");
        }

        if (Math.Abs(Plugin.Configuration.CullDistance.Value - VanillaClipDistance) <= float.Epsilon * 2)
            return;

        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            if (Monitors.FindIndex(monitor => monitor.mapCamera == camera) != -1 ||
                camera.farClipPlane                                        < Plugin.Configuration.CullDistance.Value)
                continue;

            camera.farClipPlane = Mathf.Min(camera.farClipPlane, Plugin.Configuration.CullDistance.Value);
            Plugin.Log($"Set culling distance of \"{camera.name}\" to {Plugin.Configuration.CullDistance.Value}");
        }
    }

    public void Update()
    {
        if (!Plugin.Configuration.UseAdjacentRoomTesting.Value ||
            StartOfRound.Instance.allPlayersDead              ||
            Time.time - _lastUpdateTime < 1 / Plugin.Configuration.UpdateFrequency.Value)
            return;

        _lastUpdateTime = Time.time;

        var localPlayer = GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                              ? GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript
                              : GameNetworkManager.Instance.localPlayerController;

        _enabledMonitors = Monitors.FindAll(monitor => monitor.mapCamera.enabled);

        CullOrigins.Clear();
        CullOrigins.Add(localPlayer.transform.position);

        foreach (var monitor in _enabledMonitors)
        {
            if (!monitor.targetedPlayer.isInsideFactory)
                return;

            CullOrigins.Add(monitor.targetedPlayer.transform.position);
        }

        if (CullOrigins.Count > 0)
            IncludeVisibleTiles();

        foreach (var container in LevelGenerationExtender.MeshContainers.Values)
            container.SetVisible(VisibleTilesThisFrame.ContainsKey(container.parentTile));
    }

    private static void IncludeVisibleTiles()
    {
        VisibleTilesThisFrame.Clear();

        foreach (var tile in LevelGenerationExtender.MeshContainers.Keys)
        {
            var anyOriginCaptured = CullOrigins.RemoveAll(pos => tile.Bounds.Contains(pos)) > 0;

            if (!anyOriginCaptured)
                continue;

            IncludeNearbyTiles(tile);
            break;
        }
    }

    private static void IncludeNearbyTiles(Tile origin)
    {
        var depthTesterQueue = new Queue<TileDepthTester>();
        var traversedTiles   = new HashSet<Tile>();

        depthTesterQueue.Clear();
        depthTesterQueue.Enqueue(new TileDepthTester(origin, 0));

        while (depthTesterQueue.Count > 0)
        {
            var tile = depthTesterQueue.Dequeue();

            // We use a second list here so other depth searches won't interfere
            // E.g. A is the first process, B is the second:
            //       [B3]  <<- Not traversed because B sees (A2) as done and will halt early
            // [A1]  [A2]  [A3]
            //       [B1]
            VisibleTilesThisFrame.TryAdd(tile.tile, LevelGenerationExtender.MeshContainers[tile.tile]);
            traversedTiles.Add(tile.tile);

            if (tile.iteration == Plugin.Configuration.MaxBranchingDepth.Value - 1)
                continue;

            foreach (var doorway in tile.tile.UsedDoorways)
            {
                var neighborTile = doorway.ConnectedDoorway.Tile;

                if (traversedTiles.Contains(neighborTile) ||
                    // This is possible due to traversing to a tile that has null doorways
                    !LevelGenerationExtender.MeshContainers.ContainsKey(neighborTile))
                    continue;

                depthTesterQueue.Enqueue(new TileDepthTester(neighborTile, tile.iteration + 1));
            }
        }
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
