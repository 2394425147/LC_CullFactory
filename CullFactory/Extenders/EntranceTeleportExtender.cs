using System.Collections.Generic;
using CullFactory.Behaviours;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(EntranceTeleport))]
public static class EntranceTeleportExtender
{
    // Players' tracking transform is hierarchically different from that of a radar booster
    private static readonly Dictionary<GameObject, Transform> ObjectsInsideFactory = new();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(EntranceTeleport.TeleportPlayerClientRpc))]
    private static void OnTeleport(ref int playerObj)
    {
        var player = StartOfRound.Instance.allPlayerScripts[playerObj];

        var goingInside = !player.isInsideFactory;

        if (goingInside)
        {
            if (!player.IsLocalPlayer)
                ObjectsInsideFactory.Add(player.gameObject, player.gameplayCamera.transform);

            foreach (var item in player.ItemSlots)
            {
                if (item == null ||
                    item.GetType() != typeof(RadarBoosterItem))
                    continue;

                ObjectsInsideFactory.Add(item.gameObject, item.transform);
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

        // Force an update
        if (DynamicCuller.useFactoryFarPlane)
            DynamicCuller.Instance.Update();

        Plugin.Log($"Changing far plane distance to {DynamicCuller.FocusedPlayer.gameplayCamera.farClipPlane}");
    }

    public static bool IsInsideFactory(GameObject gameObject, out Transform transform)
    {
        return ObjectsInsideFactory.TryGetValue(gameObject, out transform);
    }
}