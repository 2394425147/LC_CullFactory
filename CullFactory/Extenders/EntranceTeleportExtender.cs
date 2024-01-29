using System.Collections.Generic;
using CullFactory.Behaviours;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(EntranceTeleport))]
public static class EntranceTeleportExtender
{
    private static readonly List<Transform> ObjectsInsideFactory = new();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(EntranceTeleport.TeleportPlayerClientRpc))]
    private static void OnTeleport(ref int playerObj)
    {
        var player = StartOfRound.Instance.allPlayerScripts[playerObj];

        if (player.isInsideFactory)
        {
            if (!player.IsLocalPlayer)
                ObjectsInsideFactory.Add(player.transform);

            foreach (var item in player.ItemSlots)
            {
                if (item.GetType() != typeof(RadarBoosterItem))
                    continue;

                ObjectsInsideFactory.Add(item.transform);
            }
        }
        else
        {
            if (!player.IsLocalPlayer)
                ObjectsInsideFactory.Remove(player.transform);

            foreach (var item in player.ItemSlots)
            {
                if (item           == null ||
                    item.GetType() != typeof(RadarBoosterItem))
                    continue;

                ObjectsInsideFactory.Remove(item.transform);
            }
        }

        if (player.IsLocalPlayer)
            UpdateFarPlane();
    }

    private static void UpdateFarPlane()
    {
        if (DynamicCuller.useFactoryFarPlane == DynamicCuller.FocusedPlayer.isInsideFactory)
            return;

        DynamicCuller.useFactoryFarPlane = DynamicCuller.FocusedPlayer.isInsideFactory;
        DynamicCuller.FocusedPlayer.gameplayCamera.farClipPlane = DynamicCuller.useFactoryFarPlane
                                                                      ? Plugin.Configuration.CullDistance.Value
                                                                      : Plugin.Configuration.SurfaceCullDistance.Value;

        Plugin.Log($"Changing far plane distance to {DynamicCuller.FocusedPlayer.gameplayCamera.farClipPlane}");
    }

    public static bool IsInsideFactory(Transform transform) =>
        ObjectsInsideFactory.Contains(transform);
}
