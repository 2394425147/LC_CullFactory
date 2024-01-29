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

        var goingInside = !player.isInsideFactory;

        if (goingInside)
        {
            if (!player.IsLocalPlayer)
                ObjectsInsideFactory.Add(player.transform);

            foreach (var item in player.ItemSlots)
            {
                if (item           == null ||
                    item.GetType() != typeof(RadarBoosterItem))
                    continue;

                ObjectsInsideFactory.Add(item.transform);
                Plugin.Log($"Tracking {item.name}");
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
                Plugin.Log($"Stopped tracking {item.name}");
            }
        }

        if (player.IsLocalPlayer)
            UpdateFarPlane();
    }

    private static void UpdateFarPlane()
    {
        var goingInside = !DynamicCuller.FocusedPlayer.isInsideFactory;

        if (DynamicCuller.useFactoryFarPlane == !goingInside)
            return;

        DynamicCuller.useFactoryFarPlane = !goingInside;
        DynamicCuller.FocusedPlayer.gameplayCamera.farClipPlane = DynamicCuller.useFactoryFarPlane
                                                                      ? Plugin.Configuration.CullDistance.Value
                                                                      : Plugin.Configuration.SurfaceCullDistance.Value;

        Plugin.Log($"Changing far plane distance to {DynamicCuller.FocusedPlayer.gameplayCamera.farClipPlane}");
    }

    public static bool IsInsideFactory(Transform transform) =>
        ObjectsInsideFactory.Contains(transform);
}
