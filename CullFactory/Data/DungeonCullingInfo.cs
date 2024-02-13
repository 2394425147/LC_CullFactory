using System;
using System.Collections.Generic;
using System.Linq;
using DunGen;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace CullFactory.Data;

public static class DungeonCullingInfo
{
    private const float OutsideTileRadius = 10f;
    private const float SqrOutsideTileRadius = OutsideTileRadius * OutsideTileRadius;

    private const float AdjacentTileIntrusionDistance = 0.2f;

    public static Dictionary<Doorway, Portal> AllPortals;
    public static TileContents[] AllTileContents { get; private set; }
    public static Dictionary<Tile, TileContents> TileContentsForTile { get; private set; }
    public static int AllTileLayersMask = 0;

    public static void OnLevelGenerated()
    {
        var interiorName = RoundManager.Instance.dungeonGenerator.Generator.DungeonFlow.name;
        Plugin.LogAlways($"{interiorName} has finished generating with seed {StartOfRound.Instance.randomMapSeed}.");

        var derivePortalBoundsFromTile = Array.IndexOf(Config.InteriorsWithFallbackPortals, interiorName) != -1;
        if (derivePortalBoundsFromTile)
            Plugin.LogAlways($"Using tile bounds to determine the size of portals for {interiorName}.");

        var startTime = Time.realtimeSinceStartupAsDouble;
        CreatePortals(derivePortalBoundsFromTile);
        Plugin.Log($"Preparing portal information for the dungeon took {(Time.realtimeSinceStartupAsDouble - startTime) * 1000:0.###}ms");

        startTime = Time.realtimeSinceStartupAsDouble;
        CollectAllTileContents();
        Plugin.Log($"Preparing tile information for the dungeon took {(Time.realtimeSinceStartupAsDouble - startTime) * 1000:0.###}ms");
    }

    public static void UpdateInteriorsWithFallbackPortals()
    {
        if (AllPortals != null)
            OnLevelGenerated();
    }

    private static void CreatePortals(bool useTileBounds)
    {
        var connections = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.Connections;
        AllPortals = new Dictionary<Doorway, Portal>(connections.Count);

        foreach (var doorConnection in connections)
        {
            AllPortals[doorConnection.A] = new Portal(doorConnection.A, useTileBounds);
            AllPortals[doorConnection.B] = new Portal(doorConnection.B, useTileBounds);
        }
    }

    private static void CollectContentsIntoTile(Component parent, TileContentsBuilder builder)
    {
        builder.renderers.UnionWith(parent.GetComponentsInChildren<Renderer>());
        builder.lights.UnionWith(parent.GetComponentsInChildren<Light>());

        var syncedObjectSpawners = parent.GetComponentsInChildren<SpawnSyncedObject>();
        foreach (var spawner in syncedObjectSpawners)
        {
            builder.renderers.UnionWith(spawner.GetComponentsInChildren<Renderer>());
            builder.lights.UnionWith(spawner.GetComponentsInChildren<Light>());
        }
    }

    private static void CollectAllTileContents()
    {
        var tiles = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles;
        TileContentsForTile = new Dictionary<Tile, TileContents>(tiles.Count);
        AllTileLayersMask = 0;

        var tileContentsBuilders = new Dictionary<Tile, TileContentsBuilder>();

        foreach (var tile in tiles)
        {
            var builder = new TileContentsBuilder(tile);

            // Get objects within the current tile.
            CollectContentsIntoTile(tile, builder);

            // Create a mask containing all the layers that are used by the contents of tiles.
            foreach (var renderer in builder.renderers)
                AllTileLayersMask |= 1 << renderer.gameObject.layer;
            foreach (var light in builder.lights)
                AllTileLayersMask |= light.cullingMask;

            // Get the doors that this tile is connected to. Otherwise, they may pop in and out when the edge of the view
            // frustum is at the edge of the portal.
            foreach (var doorway in tile.UsedDoorways)
            {
                if (doorway.doorComponent == null)
                    continue;
                CollectContentsIntoTile(doorway.doorComponent, builder);
            }

            tileContentsBuilders[tile] = builder;
        }

        // Get objects in neighboring tiles that overlap with this tile. Doors often overlap,
        // but floor decals in the factory interior can as well.
        foreach (var tile in tiles)
        {
            var builder = tileContentsBuilders[tile];

            var overlappingTileBounds = tile.Bounds;
            overlappingTileBounds.extents -= new Vector3(AdjacentTileIntrusionDistance, AdjacentTileIntrusionDistance,
                                                         AdjacentTileIntrusionDistance);

            foreach (var doorway in tile.UsedDoorways)
            {
                var adjacentTile = doorway.connectedDoorway?.tile;
                if (adjacentTile == null)
                    continue;
                builder.renderers.UnionWith(tileContentsBuilders[adjacentTile].renderers
                                                                              .Where(renderer =>
                                                                                         renderer.bounds
                                                                                             .Intersects(overlappingTileBounds)));
            }
        }

        // Collect all external lights that may influence the tiles that we know of:
        foreach (var (builder, light) in
                 tileContentsBuilders.Values.SelectMany(builder => builder.lights.Select(light => (builder, light))))
        {
            var lightRangeSquared = light.range * light.range;

            var hasShadows = light.shadows != LightShadows.None;
            var lightPassesThroughWalls = light.GetComponent<HDAdditionalLightData>() is { shadowDimmer: < 1 };

            foreach (var tile in tiles)
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
                    tileContentsBuilder.externalLightOccluders.UnionWith(builder.renderers);
            }

            // If the light has shadows, use our portals to determine where it can reach.
            if (!hasShadows)
                continue;

            CallForEachLineOfSight(light.transform.position, builder.tile, (tiles, index) =>
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
                    lastTileContentsBuilder.externalLightOccluders.UnionWith(tileContentsBuilders[tiles[i]].renderers);

                // If the light can't pass through walls, then it hasn't been added to the
                // list of external lights affecting this tile yet. Add it now.
                if (!lightPassesThroughWalls)
                    lastTileContentsBuilder.externalLights.Add(light);
            });
        }

        foreach (var pair in tileContentsBuilders)
            TileContentsForTile[pair.Key] = pair.Value.Build();

        AllTileContents = new TileContents[TileContentsForTile.Count];
        var i = 0;
        foreach (var tileContents in TileContentsForTile.Values)
            AllTileContents[i++] = tileContents;
    }

    public static TileContents GetTileContents(this Vector3 point)
    {
        var sqrClosestTileDistance = SqrOutsideTileRadius;
        TileContents closestTileContents = null;

        foreach (var tileContents in AllTileContents)
        {
            if (tileContents.bounds.Contains(point))
                return tileContents;

            var sqrTileDistance = tileContents.bounds.SqrDistance(point);

            if (sqrTileDistance > sqrClosestTileDistance)
                continue;

            sqrClosestTileDistance = sqrTileDistance;
            closestTileContents = tileContents;
        }

        return closestTileContents;
    }

    public static void CollectAllTilesWithinCameraFrustum(Camera camera, List<TileContents> intoList)
    {
        var frustum = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var tileContents in AllTileContents)
        {
            if (GeometryUtility.TestPlanesAABB(frustum, tileContents.bounds))
                intoList.Add(tileContents);
        }
    }

    private const int MaxStackCapacity = 15;
    private static readonly Tile[] TileStack = new Tile[MaxStackCapacity];
    private static readonly int[] IndexStack = new int[MaxStackCapacity];
    private static readonly Plane[][] FrustumStack = new Plane[MaxStackCapacity][];

    public delegate void LineOfSightCallback(Tile[] tileStack, int stackIndex);

    public static void CallForEachLineOfSight(Vector3 origin, Tile originTile, Plane[] frustum, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        FrustumStack[0] = frustum;
        var stackIndex = 0;

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
            var connectedTile = doorway.ConnectedDoorway?.Tile;

            if (connectedTile == null)
                continue;
            if (stackIndex > 0 && ReferenceEquals(connectedTile, TileStack[stackIndex - 1]))
                continue;

            var portal = AllPortals[doorway];

            var outsideFrustum = false;
            for (var i = 0; i <= stackIndex; i++)
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

            TileStack[stackIndex] = connectedTile;
            IndexStack[stackIndex] = 0;

            if (FrustumStack[stackIndex] is null)
                FrustumStack[stackIndex] = portal.GetFrustumPlanes(origin);
            else
                portal.GetFrustumPlanesNonAlloc(origin, FrustumStack[stackIndex]);

            callback(TileStack, stackIndex);
        }
    }

    public static void CallForEachLineOfSight(Camera camera, Tile originTile, LineOfSightCallback callback)
    {
        CallForEachLineOfSight(camera.transform.position, originTile, GeometryUtility.CalculateFrustumPlanes(camera), callback);
    }

    public static void CallForEachLineOfSight(Vector3 origin, Tile originTile, LineOfSightCallback callback)
    {
        CallForEachLineOfSight(origin, originTile, [], callback);
    }
}
