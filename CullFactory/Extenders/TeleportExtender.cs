using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(EntranceTeleport))]
public static class TeleportExtender
{
    // Players' tracking transform is hierarchically different from that of a radar booster
    private static readonly Dictionary<GameObject, Transform> ObjectsInsideFactory = [];

    private static float[] playerGameplayCameraFarPlanes;

    public static void SetInitialFarClipPlane()
    {
        var allPlayers = StartOfRound.Instance.allPlayerScripts;

        if (!Config.CullDistanceEnabled.Value)
        {
            if (playerGameplayCameraFarPlanes == null)
                return;
            for (int i = 0; i < allPlayers.Length; i++)
                allPlayers[i].gameplayCamera.farClipPlane = playerGameplayCameraFarPlanes[i];
            return;
        }

        playerGameplayCameraFarPlanes = new float[allPlayers.Length];

        for (int i = 0; i < allPlayers.Length; i++)
        {
            var player = allPlayers[i];
            playerGameplayCameraFarPlanes[i] = player.gameplayCamera.farClipPlane;
            player.gameplayCamera.farClipPlane = player.isInsideFactory
                                                     ? Config.CullDistance.Value
                                                     : Config.SurfaceCullDistance.Value;
            Plugin.Log($"Set culling distance of \"{player.gameplayCamera.name}\" to {player.gameplayCamera.farClipPlane}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.TeleportPlayer))]
    private static void OnTeleportPlayerController(PlayerControllerB __instance)
    {
        OnPlayerTeleported(__instance);
    }

    // EntranceTeleport sets the `isInsideFactory` flag after calling `PlayerControllerB.TeleportPlayer()`,
    // so we need to postfix these too.

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayer))]
    private static void OnTeleportLocalPlayerThroughEntrance()
    {
        OnPlayerTeleported(StartOfRound.Instance.localPlayerController);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayerClientRpc))]
    private static void OnTeleportOtherPlayerThroughEntrance(ref int playerObj)
    {
        var player = StartOfRound.Instance.allPlayerScripts[playerObj];
        if (player == StartOfRound.Instance.localPlayerController)
            return;
        OnPlayerTeleported(player);
    }

    private static void OnPlayerTeleported(PlayerControllerB player)
    {
        if (player.isInsideFactory)
        {
            if (!player.IsLocalPlayer)
                ObjectsInsideFactory[player.gameObject] = player.gameplayCamera.transform;

            foreach (var item in player.ItemSlots)
            {
                if (item == null ||
                    item.GetType() != typeof(RadarBoosterItem))
                    continue;

                ObjectsInsideFactory[item.gameObject] = item.transform;
                Plugin.Log($"Tracking {item.name}");
            }
        }
        else
        {
            if (!player.IsLocalPlayer)
                ObjectsInsideFactory.Remove(player.gameObject);

            foreach (var item in player.ItemSlots)
            {
                if (item == null ||
                    item.GetType() != typeof(RadarBoosterItem))
                    continue;

                ObjectsInsideFactory.Remove(item.gameObject);
                Plugin.Log($"Stopped tracking {item.name}");
            }
        }

        UpdateFarPlane(player);
    }

    private static void UpdateFarPlane(PlayerControllerB player)
    {
        if (!Config.CullDistanceEnabled.Value)
            return;

        player.gameplayCamera.farClipPlane = player.isInsideFactory
                                                 ? Config.CullDistance.Value
                                                 : Config.SurfaceCullDistance.Value;

        Plugin.Log($"{player.playerUsername} is{(player.isInsideFactory ? "" : " not")} in the factory, set far plane distance to {player.gameplayCamera.farClipPlane}");
    }

    public static bool IsInsideFactory(GameObject gameObject, out Transform transform)
    {
        return ObjectsInsideFactory.TryGetValue(gameObject, out transform);
    }
}