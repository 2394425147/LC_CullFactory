using CullFactory.Behaviours.CullingMethods;
using CullFactory.Data;
using HarmonyLib;

namespace CullFactory.Extenders;

public static class LevelGenerationExtender
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SetLevelObjectVariables))]
    private static void OnLevelGenerated()
    {
        DungeonCullingInfo.OnLevelGenerated();

        CullingMethod.Initialize();
        Plugin.CreateCullingVisualizers();

        DynamicObjects.CollectAllTrackedObjects();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.PassTimeToNextDay))]
    private static void OnRoundEnded()
    {
        DungeonCullingInfo.ClearAll();

        DynamicObjects.CollectAllTrackedObjects();
    }
}
