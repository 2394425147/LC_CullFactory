using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CullFactory.Behaviours.CullingMethods;
using CullFactory.Data;
using DunGen;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(RoundManager))]
public sealed class LevelGenerationExtender
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(RoundManager.waitForMainEntranceTeleportToSpawn))]
    private static void OnLevelGenerated()
    {
        DungeonCullingInfo.OnLevelGenerated();

        CullingMethod.Initialize();
        Plugin.CreateCullingVisualizers();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RoundManager.DespawnPropsAtEndOfRound))]
    private static void OnPropsDespawned()
    {
        DynamicObjects.CollectAllTrackedObjects();
    }
}
