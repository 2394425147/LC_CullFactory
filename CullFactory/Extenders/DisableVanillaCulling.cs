using System.Collections.Generic;
using DunGen;
using HarmonyLib;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(StartOfRound))]
public static class DisableVanillaCulling
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.Start))]
    private static void OnOcclusionCullerLoaded(StartOfRound __instance)
    {
        __instance.occlusionCuller.enabled = false;
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.UpdateOcclusionCuller))]
    private static void OnOcclusionCullerPositionChanged(StartOfRound __instance)
    {
        var culler = __instance.occlusionCuller;
        if (culler.currentTile == null)
            culler.currentTile = culler.FindCurrentTile();
        else if (!culler.currentTile.Bounds.Contains(culler.targetTransform.position))
            culler.currentTile = culler.SearchForNewCurrentTile();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdjacentRoomCullingModified), nameof(AdjacentRoomCullingModified.UpdateRendererLists), [ typeof(List<Tile>), typeof(List<Door>) ])]
    private static bool StopCollectingAndHidingTiles()
    {
        return false;
    }
}
