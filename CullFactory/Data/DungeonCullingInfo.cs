using System;
using System.Collections.Generic;
using System.Linq;
using CullFactory.Services;
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

    public static Light[] AllLightsInDungeon { get; private set; }

    public static Bounds DungeonBounds;

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

        DynamicObjects.CollectAllTrackedObjects();
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
        builder.renderers.UnionWith(parent.GetComponentsInChildren<Renderer>(includeInactive: true));
        builder.lights.UnionWith(parent.GetComponentsInChildren<Light>(includeInactive: true));
    }

    private static void CollectAllTileContents()
    {
        var tiles = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles;
        TileContentsForTile = new Dictionary<Tile, TileContents>(tiles.Count);
        AllTileLayersMask = 0;

        var tileContentsBuilders = new Dictionary<Tile, TileContentsBuilder>();
        var lightsInDungeon = new List<Light>();

        var dungeonMin = Vector3.positiveInfinity;
        var dungeonMax = Vector3.negativeInfinity;

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
            lightsInDungeon.AddRange(builder.lights);
            dungeonMin = Vector3.Min(dungeonMin, builder.bounds.min);
            dungeonMax = Vector3.Max(dungeonMax, builder.bounds.max);
        }

        AllLightsInDungeon = [.. lightsInDungeon];
        DungeonBounds.min = dungeonMin;
        DungeonBounds.max = dungeonMax;

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
            var hasShadows = light.shadows != LightShadows.None;
            var lightPassesThroughWalls = !hasShadows || light.GetComponent<HDAdditionalLightData>() is { shadowDimmer: < 1 };

            if (lightPassesThroughWalls)
            {
                foreach (var tile in tiles)
                {
                    var tileContentsBuilder = tileContentsBuilders[tile];
                    if (!light.Affects(tile.Bounds))
                        continue;

                    // If the light has no shadows or if the shadows don't fully occlude light,
                    // it can pass through walls. Add it to the list of external lights affecting
                    // all tiles in its range.
                    tileContentsBuilder.externalLights.Add(light);

                    // If we're adding the light to external lights above but it has shadows,
                    // we need to occlude its light fully so that it only shines through its
                    // portals, or it will shine through walls brightly.
                    if (hasShadows)
                        tileContentsBuilder.externalLightOccluders.UnionWith(builder.renderers);
                }
            }

            // If the light has shadows, use our portals to determine where it can reach.
            if (!hasShadows)
                continue;

            VisibilityTesting.CallForEachLineOfSight(light.transform.position, builder.tile, (tiles, frustums, index) =>
            {
                if (index < 1)
                    return;

                var currentTile = tiles[index];
                var currentTileBuilder = tileContentsBuilders[currentTile];

                if (!light.Affects(currentTile.Bounds))
                    return;

                // Store any tiles that may occlude the light on its path to the current tile.
                for (var previousTileIndex = index - 1; previousTileIndex >= 0; previousTileIndex--)
                {
                    var previousTile = tiles[previousTileIndex];
                    var previousTileBuilder = tileContentsBuilders[previousTile];

                    foreach (var renderer in previousTileBuilder.renderers)
                    {
                        var occluderBounds = renderer.bounds;
                        if (!light.Affects(occluderBounds))
                            continue;

                        if (!occluderBounds.IntersectsFrustums(frustums, previousTileIndex))
                            continue;
                        currentTileBuilder.externalLightOccluders.Add(renderer);
                    }
                }

                // If the light can't pass through walls, then it hasn't been added to the
                // list of external lights affecting this tile yet. Add it now.
                if (!lightPassesThroughWalls)
                    currentTileBuilder.externalLights.Add(light);
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
}
