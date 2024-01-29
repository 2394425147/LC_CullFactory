using CullFactory.Behaviours;
using HarmonyLib;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(EntranceTeleport))]
public sealed class EntranceTeleportExtender
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(EntranceTeleport.TeleportPlayerServerRpc))]
    private static void OnTeleport()
    {
        if (DynamicCuller.useFactoryFarPlane == DynamicCuller.FocusedPlayer.isInsideFactory)
            return;

        DynamicCuller.useFactoryFarPlane = DynamicCuller.FocusedPlayer.isInsideFactory;
        DynamicCuller.FocusedPlayer.gameplayCamera.farClipPlane = DynamicCuller.useFactoryFarPlane
                                                                      ? Plugin.Configuration.CullDistance.Value
                                                                      : Plugin.Configuration.SurfaceCullDistance.Value;

        Plugin.Log($"Changing far plane distance to {DynamicCuller.FocusedPlayer.gameplayCamera.farClipPlane}");
    }
}
