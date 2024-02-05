using System.Collections.Generic;
using DunGen;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

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

        var tileContentsBuilders = new Dictionary<Tile, TileContentsBuilder>();
        var allLights = new List<Light>();

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

            allLights.AddRange(builder.lights);

            tileContentsBuilders[tile] = builder;
        }

        // Collect all external lights that may influence the tiles that we know of:
        foreach (var light in allLights)
        {
            var lightTile = light.transform.position.GetTile();
            if (lightTile == null)
            {
                Plugin.Log($"Light '{light.name}' was outside of the dungeon.");
                continue;
            }

            var lightRangeSquared = light.range * light.range;

            var hasShadows = light.shadows != LightShadows.None;
            var lightPassesThroughWalls = light.GetComponent<HDAdditionalLightData>() is HDAdditionalLightData hdLight && hdLight.shadowDimmer < 1;

            foreach (var tile in AllTiles)
            {
                var tileContentsBuilder = tileContentsBuilders[tile];
                var bounds = tile.Bounds;
                var lightDistanceSquared = bounds.SqrDistance(light.transform.position);
                if (lightDistanceSquared > lightRangeSquared)
                    continue;

                // If the light has no shadows or if the shadows don't fully occlude light,
                // it can pass through walls. Add it to the list of external lights affecting
                // all tiles in its range.
                if (!hasShadows || lightPassesThroughWalls)
                    tileContentsBuilder.externalLights.Add(light);

                // If the light casts shadows but still passes through walls, then it will
                // be enabled when this tile is visible. However, since the room it is in
                // occludes the light leaving it, we must ensure that room is rendered to
                // occlude that light.
                if (hasShadows)
                    tileContentsBuilder.externalLightOccluders.AddRange(tileContentsBuilders[lightTile].renderers);
            }

            // If the light has shadows, use our portals to determine where it can reach.
            if (!hasShadows)
                continue;

            CallForEachLineOfSight(light.transform.position, lightTile, (tiles, index) =>
            {
                if (index < 1)
                    return;

                var lastTile = tiles[index];
                var lastTileContentsBuilder = tileContentsBuilders[lastTile];

                var bounds = lastTile.Bounds;
                var lightDistanceSquared = bounds.SqrDistance(light.transform.position);
                if (lightDistanceSquared > lightRangeSquared)
                    return;

                // Store any tiles that may occlude the light on its path to the current tile.
                for (var i = 1; i < index; i++)
                    lastTileContentsBuilder.externalLightOccluders.AddRange(tileContentsBuilders[tiles[i]].renderers);

                // If the light can't pass through walls, then it hasn't been added to the
                // list of external lights affecting this tile yet. Add it now.
                if (!lightPassesThroughWalls)
                    lastTileContentsBuilder.externalLights.Add(light);
            });
        }

        foreach (var pair in tileContentsBuilders)
            AllTileContents[pair.Key] = pair.Value.Build();
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

        while (stackIndex >= 0)
        {
            var tile = TileStack[stackIndex];
            var index = IndexStack[stackIndex]++;

            if (index >= tile.UsedDoorways.Count)
            {
                stackIndex--;
                continue;
            }

            var doorway = tile.UsedDoorways[index];

            if (stackIndex > 0 && doorway.ConnectedDoorway.Tile == TileStack[stackIndex - 1])
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
            if (stackIndex >= MaxStackCapacity)
            {
                Plugin.LogError($"Exceeded the maximum portal occlusion culling depth of {MaxStackCapacity}");
                break;
            }

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
