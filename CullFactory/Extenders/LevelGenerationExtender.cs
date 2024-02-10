using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using CullFactory.Data;
using DunGen;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(RoundManager))]
public sealed class LevelGenerationExtender
{
    public static readonly Dictionary<Tile, TileVisibility> MeshContainers = [];

    public static Dictionary<SpawnSyncedObject, GameObject> TileSyncedObjects = [];

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RoundManager.waitForMainEntranceTeleportToSpawn))]
    private static void OnLevelGenerated()
    {
        MeshContainers.Clear();

        foreach (var tile in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles)
        {
            if (tile.UsedDoorways.FindIndex(doorway => doorway == null) != -1)
            {
                Plugin.LogError($"Tile {tile.name} has a doorway that connects nowhere, skipping");
                continue;
            }

            MeshContainers.Add(tile, new TileVisibility(tile));
        }

        DungeonCullingInfo.OnLevelGenerated();
        TeleportExtender.SetInitialFarClipPlane();

        Plugin.CreateCullingHandler();
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

public class TileVisibility
{
    private readonly Light[] _lights;

    private readonly MeshRenderer[] _meshRenderers;
    public readonly Tile parentTile;

    private bool _previouslyVisible = true;

    public TileVisibility(Tile parentTile)
    {
        this.parentTile = parentTile;

        _meshRenderers = Array.FindAll(parentTile.GetComponentsInChildren<MeshRenderer>(), renderer => renderer.enabled);
        _lights = Array.FindAll(parentTile.GetComponentsInChildren<Light>(), renderer => renderer.enabled);

        Plugin.Log($"Found tile {parentTile.name} with {_meshRenderers.Length} mesh renderers and {_lights.Length} lights");
    }

    public void SetVisible(bool value)
    {
        if (_previouslyVisible == value)
            return;

        Plugin.Log(value ? $"Showing {parentTile.name}" : $"Culling {parentTile.name}");

        foreach (var meshRenderer in _meshRenderers)
            meshRenderer.enabled = value;

        foreach (var light in _lights)
            light.enabled = value;

        _previouslyVisible = value;
    }
}
