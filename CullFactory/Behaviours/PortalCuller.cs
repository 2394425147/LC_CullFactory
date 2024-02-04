using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Utilities;
using GameNetcodeStuff;
using UnityEngine;

namespace CullFactory.Behaviours;

/// <summary>
/// DynamicCuller instances are tied to each moon
/// </summary>
public sealed class PortalCuller : MonoBehaviour
{
    public static PortalCuller Instance { get; private set; }

    private static readonly HashSet<TileContents> VisibleTiles = new();

    private static float _lastUpdateTime;

    public static PlayerControllerB FocusedPlayer => GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                                                         ? GameNetworkManager.Instance.localPlayerController
                                                                             .spectatedPlayerScript
                                                         : GameNetworkManager.Instance.localPlayerController;

    public static Camera FocusedCamera => StartOfRound.Instance.activeCamera;

    private void OnEnable()
    {
        if (Instance != null)
            Destroy(Instance);

        Instance = this;
    }

    public void Update()
    {
        if (!Plugin.Configuration.UseAdjacentRoomTesting.Value ||
            StartOfRound.Instance.allPlayersDead ||
            Time.time - _lastUpdateTime < 1 / Plugin.Configuration.UpdateFrequency.Value)
            return;

        _lastUpdateTime = Time.time;

        VisibleTiles.Clear();

        if (FocusedPlayer.isInsideFactory)
            IncludeVisibleTiles();

        foreach (var tile in DungeonUtilities.AllTiles)
            tile.SetActive(VisibleTiles.Contains(tile));
    }

    private static void IncludeVisibleTiles()
    {
        VisibleTiles.Clear();

        var currentTile = FocusedCamera.transform.position.GetTile();
        VisibleTiles.Add(currentTile.GetContents());
    }

    private void OnDestroy()
    {
        Instance = null;
    }
}
