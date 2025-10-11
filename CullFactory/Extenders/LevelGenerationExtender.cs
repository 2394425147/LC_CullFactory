using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CullFactory.Data;
using DunGen;
using HarmonyLib;

namespace CullFactory.Extenders;

public static class LevelGenerationExtender
{
    private static bool PauseCullingUpdates = false;

    private static void OnLevelBeginGenerating()
    {
        PauseCullingUpdates = true;
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc))]
    private static IEnumerable<CodeInstruction> GenerateNewLevelClientRpcTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var matcher = new CodeMatcher(instructions)
            .MatchForward(false, [
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Call, typeof(RoundManager).GetMethod(nameof(RoundManager.GenerateNewFloor), [])),
            ]);
        if (matcher.IsInvalid)
        {
            Plugin.LogError("Failed to find the call to begin dungeon generation in RoundManager");
            return instructions;
        }

        return matcher
            .Insert([
                new CodeInstruction(OpCodes.Call, typeof(LevelGenerationExtender).GetMethod(nameof(OnLevelBeginGenerating), BindingFlags.NonPublic | BindingFlags.Static)),
            ])
            .Instructions();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(DungeonGenerator), nameof(DungeonGenerator.ChangeStatus))]
    private static void OnDungeonGenerated(DungeonGenerator __instance, GenerationStatus status)
    {
        if (PauseCullingUpdates)
            return;
        if (status != GenerationStatus.Complete)
            return;

        DungeonCullingInfo.OnDungeonGenerated(__instance.CurrentDungeon);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.SetLevelObjectVariables))]
    private static void OnLevelGenerated()
    {
        Plugin.LogAlways($"Generation has completed with seed {StartOfRound.Instance.randomMapSeed}.");

        PauseCullingUpdates = false;

        DungeonCullingInfo.RefreshCullingInfo();

        DynamicObjects.CollectAllTrackedObjects();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.PassTimeToNextDay))]
    private static void OnRoundEnded()
    {
        DungeonCullingInfo.CleanUpDestroyedDungeons();

        DynamicObjects.CollectAllTrackedObjects();
    }
}
