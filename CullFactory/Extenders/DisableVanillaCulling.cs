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
            culler.SearchForNewCurrentTile();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(AdjacentRoomCullingModified))]
    [HarmonyPatch(nameof(AdjacentRoomCullingModified.ClearAllDungeons))]
    [HarmonyPatch(nameof(AdjacentRoomCullingModified.AddDungeon))]
    private static bool StopCollectingAndHidingTiles()
    {
        return false;
    }
}
