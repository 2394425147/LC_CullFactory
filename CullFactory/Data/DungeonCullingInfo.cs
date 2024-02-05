using System.Collections.Generic;
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

        CollectAllTileContents();
    }

    private static void CollectContentsIntoTile(Component parent, TileContentsBuilder builder)
    {
        builder.renderers.AddRange(parent.GetComponentsInChildren<Renderer>());
        builder.lights.AddRange(parent.GetComponentsInChildren<Light>());

        var syncedObjectSpawners = parent.GetComponentsInChildren<SpawnSyncedObject>();
        foreach (var spawner in syncedObjectSpawners)
        {
            builder.renderers.AddRange(spawner.GetComponentsInChildren<Renderer>());
            builder.lights.AddRange(spawner.GetComponentsInChildren<Light>());
        }
    }

    private static void CollectAllTileContents()
    {
        AllTileContents = new Dictionary<Tile, TileContents>(AllTiles.Length);
        foreach (var tile in AllTiles)
        {
            var builder = new TileContentsBuilder(tile);
            CollectContentsIntoTile(tile, builder);

            foreach (var doorway in tile.UsedDoorways)
            {
                if (doorway.doorComponent == null)
                    continue;
                CollectContentsIntoTile(doorway.doorComponent, builder);
            }

            AllTileContents[tile] = builder.Build();
        }
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

    const int MaxStackCapacity = 10;
    static readonly Tile[] TileStack = new Tile[MaxStackCapacity];
    static readonly int[] IndexStack = new int[MaxStackCapacity];
    static readonly Plane[][] FrustumStack = new Plane[MaxStackCapacity][];

    public delegate void LineOfSightCallback(Tile[] tileStack, int stackIndex);

    public static void CallForEachLineOfSight(Vector3 origin, Tile originTile, Plane[] frustum, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        FrustumStack[0] = frustum;
        int stackIndex = 0;

        callback(TileStack, stackIndex);

        while (stackIndex >= 0 && stackIndex < MaxStackCapacity)
        {
            var tile = TileStack[stackIndex];
            var index = IndexStack[stackIndex]++;

            if (index >= tile.UsedDoorways.Count)
            {
                stackIndex--;
                continue;
            }

            var doorway = tile.UsedDoorways[index];

            if (stackIndex > 0 && (object)doorway.ConnectedDoorway.Tile == TileStack[stackIndex - 1])
                continue;

            var portal = AllPortals[doorway];

            bool outsideFrustum = false;
            for (int i = 0; i <= stackIndex; i++)
            {
                if (!GeometryUtility.TestPlanesAABB(FrustumStack[i], portal.Bounds))
                {
                    outsideFrustum = true;
                    break;
                }
            }
            if (outsideFrustum)
                continue;

            stackIndex++;
            TileStack[stackIndex] = doorway.ConnectedDoorway.Tile;
            IndexStack[stackIndex] = 0;

            if (FrustumStack[stackIndex] is null)
                FrustumStack[stackIndex] = portal.GetFrustumPlanes(origin);
            else
                portal.GetFrustumPlanes(origin, FrustumStack[stackIndex]);

            callback(TileStack, stackIndex);
        }
    }

    public static void CallForEachLineOfSight(Camera camera, Tile originTile, LineOfSightCallback callback)
    {
        CallForEachLineOfSight(camera.transform.position, originTile, GeometryUtility.CalculateFrustumPlanes(camera), callback);
    }

    public static void CallForEachLineOfSight(Vector3 origin, Tile originTile, LineOfSightCallback callback)
    {
        CallForEachLineOfSight(origin, originTile, new Plane[0], callback);
    }
}
