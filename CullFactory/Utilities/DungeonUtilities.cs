﻿using System.Collections.Generic;
using CullFactory.Behaviours;
using CullFactory.Models;
using DunGen;
using HarmonyLib;
using UnityEngine;

namespace CullFactory.Utilities;

public static class DungeonUtilities
{
    private const float OutsideTileRadius    = 1f;
    private const float SqrOutsideTileRadius = OutsideTileRadius * OutsideTileRadius;

    public static List<TileContents> AllTiles { get; private set; }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(RoundManager), nameof(RoundManager.waitForMainEntranceTeleportToSpawn))]
    private static void OnLevelGenerated()
    {
        AllTiles = new List<TileContents>(RoundManager.Instance.dungeonGenerator.Generator.targetLength);
        foreach (var tile in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles)
            AllTiles.Add(TileContents.FromTile(tile));
        AllTiles.TrimExcess();

        RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject.AddComponent<DynamicCuller>();
    }

    public static Tile GetTile(this Vector3 point)
    {
        var  sqrClosestTileDistance = SqrOutsideTileRadius;
        Tile closestTile            = null;

        foreach (var tile in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles)
        {
            if (tile.Bounds.Contains(point))
                return tile;

            var sqrTileDistance = tile.Bounds.SqrDistance(point);

            if (sqrTileDistance > sqrClosestTileDistance)
                continue;

            sqrClosestTileDistance = sqrTileDistance;
            closestTile            = tile;
        }

        return closestTile;
    }

    private static readonly Dictionary<Vector3, Proximity> Proximities = new();

    public static List<TileContents> GetTiles(this List<Vector3> points)
    {
        var result = new List<TileContents>(points.Count);

        Proximities.Clear();
        foreach (var contents in AllTiles)
        {
            foreach (var point in points)
            {
                if (contents.tile.Bounds.Contains(point))
                {
                    result.Add(contents);
                    continue;
                }

                var sqrTileDistance = contents.tile.Bounds.SqrDistance(point);

                var firstScan = !Proximities.TryGetValue(point, out var res);

                switch (firstScan)
                {
                    case false when sqrTileDistance > res.sqrDistance:
                        continue;
                    case true:
                        Proximities.Add(point, new Proximity(sqrTileDistance, contents));
                        break;
                    default:
                        Proximities[point] = new Proximity(sqrTileDistance, contents);
                        break;
                }
            }
        }

        return result;
    }
}