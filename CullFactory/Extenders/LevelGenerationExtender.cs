using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CullFactory.Data;
using DunGen;
using HarmonyLib;
using UnityEngine;

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
        var dungeonGeneratorField = typeof(RoundManager).GetField(nameof(RoundManager.dungeonGenerator));
        var matcher = new CodeMatcher(instructions)
            .MatchForward(true, [
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldc_I4_0),
                new CodeMatch(OpCodes.Call, typeof(Object).GetMethod(nameof(Object.FindObjectOfType), [typeof(bool)]).MakeGenericMethod([typeof(RuntimeDungeon)])),
                new CodeMatch(OpCodes.Stfld, dungeonGeneratorField),
                new CodeMatch(OpCodes.Ldarg_0),
                new CodeMatch(OpCodes.Ldfld, dungeonGeneratorField),
                new CodeMatch(OpCodes.Ldnull),
                new CodeMatch(OpCodes.Call, typeof(Object).GetMethod("op_Inequality", [typeof(Object), typeof(Object)])),
                new CodeMatch(OpCodes.Brfalse),
            ]);
        if (matcher.IsInvalid)
        {
            Plugin.LogError("Failed to find the call to begin dungeon generation in RoundManager");
            return instructions;
        }

        return matcher
            .Advance(1)
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
