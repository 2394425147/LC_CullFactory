using System.Collections.Generic;
using CullFactory.Behaviours;
using CullFactory.Models;
using DunGen;
using HarmonyLib;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(RoundManager))]
public sealed class LevelGenerationExtender
{
    public static readonly Dictionary<Tile, TileVisibility> MeshContainers = new();

    // Using string instead of nameof here since waitForMainEntranceTeleportToSpawn is a private method
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

        RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject.AddComponent<DynamicCuller>();
    }
}
