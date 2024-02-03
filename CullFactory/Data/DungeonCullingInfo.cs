using System.Collections.Generic;
using CullFactory.Behaviours;
using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public static class DungeonCullingInfo
{
    private const float OutsideTileRadius = 1f;
    private const float SqrOutsideTileRadius = OutsideTileRadius * OutsideTileRadius;

    public static Dictionary<Doorway, Portal> AllPortals = [];
    public static Tile[] AllTiles { get; private set; }
    public static Dictionary<Tile, TileContents> AllTileContents { get; private set; }

    public static void OnLevelGenerated()
    {
        CreatePortals();

        AllTiles = [.. RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles];

        AllTileContents = new Dictionary<Tile, TileContents>(AllTiles.Length);
        foreach (var tile in AllTiles)
            AllTileContents[tile] = new TileContents(tile);

        RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject.AddComponent<DynamicCuller>();
    }

    private static void CreatePortals()
    {
        AllPortals.Clear();

        foreach (var doorConnection in RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.Connections)
        {
            AllPortals[doorConnection.A] = new Portal(doorConnection.A);
            AllPortals[doorConnection.B] = new Portal(doorConnection.B);
        }
    }

    public static Tile GetTile(this Vector3 point)
    {
        var sqrClosestTileDistance = SqrOutsideTileRadius;
        Tile closestTile = null;

        foreach (var tile in AllTiles)
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
}
