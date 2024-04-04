using System;
using System.Collections.Generic;
using System.Linq;
using CullFactory.Services;
using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public static class DungeonCullingInfo
{
    private const float OutsideTileRadius = 10f;
    private const float SqrOutsideTileRadius = OutsideTileRadius * OutsideTileRadius;

    private const float AdjacentTileIntrusionDistance = 0.2f;

    public static TileContents[] AllTileContents { get; private set; }
    public static Dictionary<Tile, TileContents> TileContentsForTile { get; private set; }

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
        CollectAllTileContents(derivePortalBoundsFromTile);
        Plugin.Log($"Preparing tile information for the dungeon took {(Time.realtimeSinceStartupAsDouble - startTime) * 1000:0.###}ms");
    }

    public static void ClearAll()
    {
        if (AllTileContents == null)
            return;

        AllTileContents = [];
        TileContentsForTile.Clear();
        AllLightsInDungeon = [];
        DungeonBounds = default;
    }

    public static void RefreshCullingInfo()
    {
        if (AllTileContents != null)
            OnLevelGenerated();
    }

    private static void CollectAllTileContents(bool derivePortalBoundsFromTile)
    {
        // Create TileContents instances for each tile, calculating the total dungeon
        // bounding box, as well as collecting all lights within the dungeon.
        var tiles = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles;
        TileContentsForTile = new Dictionary<Tile, TileContents>(tiles.Count);

        var lightsInDungeon = new List<Light>();

        var dungeonMin = Vector3.positiveInfinity;
        var dungeonMax = Vector3.negativeInfinity;

        foreach (var tile in tiles)
        {
            var tileContents = new TileContents(tile);
            TileContentsForTile[tile] = tileContents;
            lightsInDungeon.AddRange(tileContents.lights);

            dungeonMin = Vector3.Min(dungeonMin, tileContents.bounds.min);
            dungeonMax = Vector3.Max(dungeonMax, tileContents.bounds.max);
        }

        // Make an array of all TileContents instances, and create portals for all tiles, which will refer
        // to the TileContents instance that is accessible through them.
        AllTileContents = new TileContents[TileContentsForTile.Count];
        var i = 0;
        foreach (var tileContents in TileContentsForTile.Values)
        {
            var doorways = tileContents.tile.UsedDoorways;
            var doorwayCount = doorways.Count;
            var portals = new List<Portal>(doorwayCount);
            for (var j = 0; j < doorwayCount; j++)
            {
                var doorway = doorways[j];
                if (doorway.ConnectedDoorway == null)
                    continue;
                portals.Add(new Portal(doorway, derivePortalBoundsFromTile, TileContentsForTile[doorway.ConnectedDoorway.Tile]));
            }
            tileContents.portals = [.. portals];

            AllTileContents[i++] = tileContents;
        }

        AllLightsInDungeon = [.. lightsInDungeon];
        DungeonBounds.min = dungeonMin;
        DungeonBounds.max = dungeonMax;

        // Get objects in neighboring tiles that overlap with this tile. Doors often overlap,
        // but floor decals in the factory interior can as well.
        foreach (var tile in AllTileContents)
        {
            var overlappingTileBounds = tile.bounds;
            overlappingTileBounds.extents -= new Vector3(AdjacentTileIntrusionDistance, AdjacentTileIntrusionDistance,
                                                         AdjacentTileIntrusionDistance);
            var externalRenderers = new List<Renderer>();

            foreach (var portal in tile.portals)
            {
                var adjacentTile = portal.NextTile;
                if (adjacentTile == null)
                    continue;
                foreach (var externalRenderer in adjacentTile.renderers)
                {
                    if (!externalRenderer.bounds.Intersects(overlappingTileBounds))
                        continue;
                    externalRenderers.Add(externalRenderer);
                }
            }

            tile.externalRenderers = [.. externalRenderers];
        }

        // Collect all external lights that may influence the tiles that we know of:
        foreach (var (tile, light) in
                 AllTileContents.SelectMany(tile => tile.lights.Select(light => (tile, light))))
        {
            // Check activeInHierarchy, because isActiveAndEnabled is always false after generation.
            if (!light.gameObject.activeInHierarchy)
                continue;

            var hasShadows = light.HasShadows();

            // If we don't force the shadow fade distance to match the light fade distance, lights will
            // always be able to shine through walls if there is a long enough line of sight to a place
            // the light shines onto.
            var lightPassesThroughWalls = !Config.DisableShadowDistanceFading.Value || light.PassesThroughOccluders();

            if (lightPassesThroughWalls)
            {
                foreach (var otherTile in AllTileContents)
                {
                    if (!light.Affects(otherTile.bounds))
                        continue;

                    // If the light has no shadows or if the shadows don't fully occlude light,
                    // it can pass through walls. Add it to the list of external lights affecting
                    // all tiles in its range.
                    otherTile.externalLights = [.. otherTile.externalLights, light];

                    // If we're adding the light to external lights above but it has shadows,
                    // we need to occlude its light fully so that it only shines through its
                    // portals, or it will shine through walls brightly.
                    if (hasShadows)
                        otherTile.externalRenderers = [.. otherTile.externalRenderers.Union(tile.renderers)];
                }
            }

            // If the light has shadows, use our portals to determine where it can reach.
            if (!hasShadows)
                continue;

            var lightOrigin = light.transform.position;
            VisibilityTesting.CallForEachLineOfSight(lightOrigin, tile, (tiles, frustums, index) =>
            {
                if (index < 1)
                    return;

                var currentTile = tiles[index];

                if (!light.Affects(currentTile.bounds))
                    return;

                bool influencesARenderer = false;
                var lightOccluders = new List<Renderer>();

                // Store any tiles that may occlude the light on its path to the current tile.
                for (var previousTileIndex = index - 1; previousTileIndex >= 0; previousTileIndex--)
                {
                    var previousTile = tiles[previousTileIndex];

                    foreach (var renderer in previousTile.renderers)
                    {
                        var occluderBounds = renderer.bounds;
                        if (!light.Affects(occluderBounds))
                            continue;

                        if (!occluderBounds.IntersectsFrustums(frustums, previousTileIndex))
                            continue;

                        influencesARenderer = true;
                        lightOccluders.Add(renderer);
                    }
                }

                currentTile.externalRenderers = [.. currentTile.externalRenderers.Union(lightOccluders)];

                if (!influencesARenderer)
                    return;

                var lineOfSight = new List<Plane>();
                for (var i = 1; i <= index; i++)
                    lineOfSight.AddRange(frustums[i]);
                lineOfSight.AddRange(currentTile.bounds.GetFarthestPlanes(lightOrigin));
                currentTile.externalLightLinesOfSight = [.. currentTile.externalLightLinesOfSight, [.. lineOfSight]];

                // If the light can't pass through walls, then it hasn't been added to the
                // list of external lights affecting this tile yet. Add it now.
                if (!lightPassesThroughWalls)
                    currentTile.externalLights = [.. currentTile.externalLights, light];
            });
        }
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
