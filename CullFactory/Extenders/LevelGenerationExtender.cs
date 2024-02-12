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
    public static Dictionary<SpawnSyncedObject, GameObject> TileSyncedObjects = [];

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RoundManager.waitForMainEntranceTeleportToSpawn))]
    private static void OnLevelGenerated()
    {
        DungeonCullingInfo.OnLevelGenerated();
        TeleportExtender.SetInitialFarClipPlane();

        CullingMethod.Initialize();
        Plugin.CreateCullingVisualizers();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(RoundManager.SpawnSyncedProps))]
    private static IEnumerable<CodeInstruction> SpawnSyncedPropsTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var instructionsList = new List<CodeInstruction>(instructions);

        // At the start of the function, ensure that TileSyncedObjects is cleared so that we don't have any old references.
        instructionsList.InsertRange(0, new CodeInstruction[]
        {
            CodeInstruction.LoadField(typeof(LevelGenerationExtender), nameof(TileSyncedObjects)),
            new(OpCodes.Call, typeof(Dictionary<Tile, List<GameObject>>).GetMethod("Clear")),
        });

        var loadSyncedObject = instructionsList.FindIndex(insn => insn.opcode == OpCodes.Ldelem_Ref);

        var instantiateObject = instructionsList.FindIndex(insn => insn.operand is MethodInfo method && method.Name == nameof(UnityEngine.Object.Instantiate) && method.ReturnType == typeof(GameObject));

        // When the following happens:
        //   GameObject syncedObject = UnityEngine.Object.Instantiate(spawners[i].spawnPrefab, spawners[i].transform.position, spawners[i].transform.rotation, mapPropsContainer.transform);
        // Insert instructions to also
        //   LevelGenerationExtender.OnSyncedObjectSpawned(syncedObject, spawners[i]);
        // This allows us to track down renderers and lights for synced objects which are otherwise not in the hierarchy of a DunGen Tile.
        instructionsList.InsertRange(instantiateObject + 1, new CodeInstruction[]
        {
            new(OpCodes.Dup),
            instructionsList[loadSyncedObject - 2],
            instructionsList[loadSyncedObject - 1],
            instructionsList[loadSyncedObject],
            CodeInstruction.Call(typeof(LevelGenerationExtender), nameof(OnSyncedObjectSpawned))
        });

        return instructionsList;
    }

    private static void OnSyncedObjectSpawned(GameObject spawnedObject, SpawnSyncedObject spawner)
    {
        TileSyncedObjects[spawner] = spawnedObject;
    }
}
