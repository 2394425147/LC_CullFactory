using System.Collections.Generic;
using CullFactory.Behaviours;
using CullFactory.Data;
using DunGen;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Utilities;

public static class DungeonUtilities
{
    private const float OutsideTileRadius = 1f;
    private const float SqrOutsideTileRadius = OutsideTileRadius * OutsideTileRadius;

    public static readonly List<TileContents> AllTiles = new();
    public static readonly Dictionary<Tile, TileContents> Mapping = new();

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.waitForMainEntranceTeleportToSpawn))]
    private static void OnLevelGenerated()
    {
        AllTiles.Clear();
        Mapping.Clear();

        foreach (var tile in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles)
        {
            var tileContents = tile.GetContents();

            AllTiles.Add(tileContents);
            Mapping.Add(tile, tileContents);
        }

        AllTiles.TrimExcess();

        RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject.AddComponent<PortalCuller>();
    }

    public static Tile GetTile(this Vector3 point)
    {
        var sqrClosestTileDistance = SqrOutsideTileRadius;
        Tile closestTile = null;

        foreach (var tile in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles)
        {
            if (tile.Bounds.Contains(point))
                return tile;

            var sqrTileDistance = tile.Bounds.SqrDistance(point);

            if (sqrTileDistance > sqrClosestTileDistance)
                continue;

            sqrClosestTileDistance = sqrTileDistance;
            closestTile = tile;
        }

        return closestTile;
    }

    public static TileContents GetContents(this Tile tile)
    {
        return Mapping.TryGetValue(tile, out var result) ? result : null;
    }
}
