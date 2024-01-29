﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CullFactory.Extenders;
using DunGen;
using GameNetcodeStuff;
using UnityEngine;
using Object = UnityEngine.Object;

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
    private static Vector3                    _lastKnownPlayerPosition;
    public static  bool                       useFactoryFarPlane;

    public static PlayerControllerB FocusedPlayer => GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                                                         ? GameNetworkManager.Instance.localPlayerController
                                                                             .spectatedPlayerScript
                                                         : GameNetworkManager.Instance.localPlayerController;

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

        if (Math.Abs(Plugin.Configuration.SurfaceCullDistance.Value - VanillaClipDistance) <= float.Epsilon * 2)
            return;

        foreach (var camera in FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            var isMonitorCamera = Monitors.FindIndex(monitor => monitor.mapCamera == camera) != -1;

            if (isMonitorCamera ||
                Math.Abs(camera.farClipPlane - VanillaClipDistance) > float.Epsilon * 2)
                continue;

            camera.farClipPlane = Mathf.Min(camera.farClipPlane, Plugin.Configuration.SurfaceCullDistance.Value);
            Plugin.Log($"Set culling distance of \"{camera.name}\" to {Plugin.Configuration.SurfaceCullDistance.Value}");
        }
    }

    public void Update()
    {
        if (!Plugin.Configuration.UseAdjacentRoomTesting.Value ||
            StartOfRound.Instance.allPlayersDead               ||
            Time.time - _lastUpdateTime < 1 / Plugin.Configuration.UpdateFrequency.Value)
            return;

        _lastUpdateTime = Time.time;

        _enabledMonitors = Monitors.FindAll(monitor => monitor.mapCamera.enabled);

        CullOrigins.Clear();

        foreach (var monitor in _enabledMonitors)
        {
            if (!EntranceTeleportExtender.IsInsideFactory(monitor.radarTargets[monitor.targetTransformIndex].transform))
                continue;

            CullOrigins.Add(monitor.targetedPlayer.transform.position);
        }

        if (FocusedPlayer.isInsideFactory || CullOrigins.Count > 0)
            IncludeVisibleTiles();

        foreach (var container in LevelGenerationExtender.MeshContainers.Values)
            container.SetVisible(VisibleTilesThisFrame.ContainsKey(container.parentTile));
    }

    private static void IncludeVisibleTiles()
    {
        var localPlayerTested = false;

        VisibleTilesThisFrame.Clear();

        foreach (var tile in LevelGenerationExtender.MeshContainers.Keys)
        {
            var anyOriginCaptured = CullOrigins.RemoveAll(pos => tile.Bounds.Contains(pos)) > 0;

            if (FocusedPlayer.isInsideFactory && !localPlayerTested)
            {
                if (tile.Bounds.Contains(FocusedPlayer.transform.position))
                {
                    _lastKnownPlayerPosition = FocusedPlayer.transform.position;
                    anyOriginCaptured        = localPlayerTested = true;
                }
                else if (tile.Bounds.Contains(_lastKnownPlayerPosition))
                {
                    anyOriginCaptured = localPlayerTested = true;
                }
            }

            if (!anyOriginCaptured)
                continue;

            IncludeNearbyTiles(tile);
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
