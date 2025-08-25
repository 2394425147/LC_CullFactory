using CullFactory.Data;
using DunGen;
using HarmonyLib;
using Unity.Netcode;

namespace CullFactory.Extenders;

public static class LevelGenerationExtender
{
    private static bool PauseCullingUpdates = false;

    [HarmonyPrefix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc))]
    private static void OnLevelBeginGenerating(RoundManager __instance)
    {
        if (__instance.NetworkManager == null || !__instance.NetworkManager.IsListening)
            return;
        if (__instance.__rpc_exec_stage != NetworkBehaviour.__RpcExecStage.Client)
            return;

        PauseCullingUpdates = true;
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
