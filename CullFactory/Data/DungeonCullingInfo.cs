using System;
using System.Collections.Generic;
using System.Linq;
using CullFactory.Services;
using CullFactoryBurst;
using DunGen;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace CullFactory.Data;

public static class DungeonCullingInfo
{
    private const int RendererIntrusionTileDepth = 2;
    private const float RendererIntrusionDistance = 0.01f;

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

        AllTileContents = null;
        TileContentsForTile.Clear();
        AllLightsInDungeon = null;
        DungeonBounds = default;
    }

    public static void RefreshCullingInfo()
    {
        if (AllTileContents != null)
            OnLevelGenerated();
    }

    private static void AddIntersectingRenderers(Bounds bounds, HashSet<Renderer> toCollection, HashSet<TileContents> visitedTiles, TileContents currentTile, int tilesLeft)
    {
        if (!visitedTiles.Add(currentTile))
            return;

        foreach (var renderer in currentTile.renderers)
        {
            if (renderer.bounds.Intersects(bounds))
                toCollection.Add(renderer);
        }

        if (--tilesLeft > 0)
        {
            foreach (var portal in currentTile.portals)
                AddIntersectingRenderers(bounds, toCollection, visitedTiles, portal.NextTile, tilesLeft - 1);
        }

        visitedTiles.Remove(currentTile);
    }

    private static HashSet<Renderer> GetIntrudingRenderers(TileContents originTile)
    {
        HashSet<Renderer> result = [];

        var bounds = originTile.bounds;
        bounds.Expand(-RendererIntrusionDistance);

        HashSet<TileContents> visitedTiles = [originTile];

        foreach (var portal in originTile.portals)
            AddIntersectingRenderers(bounds, result, visitedTiles, portal.NextTile, RendererIntrusionTileDepth);

        return result;
    }

    internal static bool ShouldShadowFadingBeDisabledForLight(HDAdditionalLightData light)
    {
        // A heuristic to determine whether a light is "close enough" to fading the shadows
        // at the same distance as the light itself. These values are arbitrarily chosen to
        // return true for all lights in the mansion interior, where the performance impact
        // is negligible.
        // They may need adjustment if this results in a significant performance regression
        // in any other interiors.
        return light.shadowFadeDistance >= light.fadeDistance * 0.75 - 15;
    }

    internal static bool ShouldShadowFadingBeDisabledForLight(Light light)
    {
        if (!Config.DisableShadowDistanceFading.Value)
            return false;

        if (light.GetComponent<HDAdditionalLightData>() is { } hdLight)
            return ShouldShadowFadingBeDisabledForLight(hdLight);

        return true;
    }

    private static void CollectAllTileContents(bool derivePortalBoundsFromTile)
    {
        FillTileContentsCollections();

        CreateAndAssignPortals(derivePortalBoundsFromTile);

        AddIntrudingRenderersToTileContents();

        AddLightInfluencesToTileContents();
    }

    private static void FillTileContentsCollections()
    {
        var tiles = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.AllTiles;
        AllTileContents = new TileContents[tiles.Count];
        TileContentsForTile = new Dictionary<Tile, TileContents>(AllTileContents.Length);

        var lightsInDungeon = new List<Light>();

        var dungeonMin = Vector3.positiveInfinity;
        var dungeonMax = Vector3.negativeInfinity;

        var i = 0;
        foreach (var tile in tiles)
        {
            var tileContents = new TileContents(tile);
            AllTileContents[i++] = tileContents;
            TileContentsForTile[tile] = tileContents;
            lightsInDungeon.AddRange(tileContents.lights);

            dungeonMin = Vector3.Min(dungeonMin, tileContents.rendererBounds.min);
            dungeonMax = Vector3.Max(dungeonMax, tileContents.rendererBounds.max);
        }

        AllLightsInDungeon = [.. lightsInDungeon];
        DungeonBounds.min = dungeonMin;
        DungeonBounds.max = dungeonMax;
    }

    private static void CreateAndAssignPortals(bool derivePortalBoundsFromTile)
    {
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
        }
    }

    private static void AddIntrudingRenderersToTileContents()
    {
        for (var i = 0; i < AllTileContents.Length; i++)
        {
            var tile = AllTileContents[i];
            tile.renderers = [.. tile.renderers, .. GetIntrudingRenderers(AllTileContents[i])];
        }
    }

    private readonly struct LightInfluenceCollections(TileContents tileContents)
    {
        internal readonly HashSet<Renderer> _externalRenderers = new(tileContents.externalRenderers);
        internal readonly List<Light> _externalLights = new(tileContents.externalLights);
        internal readonly List<Plane[]> _externalLightLinesOfSight = new(tileContents.externalLightLinesOfSight);
    }

    private static void AddLightInfluencesToTileContents()
    {
        var lightInfluenceCollectionLookup = new Dictionary<TileContents, LightInfluenceCollections>(AllTileContents.Length);

        foreach (var tile in AllTileContents)
            lightInfluenceCollectionLookup[tile] = new(tile);

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
            var lightPassesThroughWalls = !ShouldShadowFadingBeDisabledForLight(light) || light.PassesThroughOccluders();

            if (lightPassesThroughWalls)
            {
                foreach (var otherTile in AllTileContents)
                {
                    if (!light.Affects(otherTile))
                        continue;

                    var otherTileInfluences = lightInfluenceCollectionLookup[otherTile];

                    // If the light has no shadows or if the shadows don't fully occlude light,
                    // it can pass through walls. Add it to the list of external lights affecting
                    // all tiles in its range.
                    otherTileInfluences._externalLights.Add(light);

                    // If we're adding the light to external lights above but it has shadows,
                    // we need to occlude its light fully so that it only shines through its
                    // portals, or it will shine through walls brightly.
                    if (hasShadows)
                        otherTileInfluences._externalRenderers.UnionWith(tile.renderers);
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

                if (!light.Affects(currentTile))
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

                        var intersects = true;
                        for (var i = 0; i <= previousTileIndex; i++)
                        {
                            if (!Geometry.TestPlanesAABB(in frustums[i], in occluderBounds))
                            {
                                intersects = false;
                                break;
                            }
                        }
                        if (!intersects)
                            continue;

                        influencesARenderer = true;
                        lightOccluders.Add(renderer);
                    }
                }

                var currentTileInfluences = lightInfluenceCollectionLookup[currentTile];
                currentTileInfluences._externalRenderers.UnionWith(lightOccluders);

                if (!influencesARenderer)
                    return;

                var lineOfSight = new List<Plane>();
                for (var i = 1; i <= index; i++)
                    lineOfSight.AddRange(frustums[i]);
                lineOfSight.AddRange(currentTile.bounds.GetFarthestPlanes(lightOrigin));
                currentTileInfluences._externalLightLinesOfSight.Add([.. lineOfSight]);

                // If the light can't pass through walls, then it hasn't been added to the
                // list of external lights affecting this tile yet. Add it now.
                if (!lightPassesThroughWalls)
                    currentTileInfluences._externalLights.Add(light);
            });
        }

        foreach (var tile in AllTileContents)
        {
            var influences = lightInfluenceCollectionLookup[tile];
            tile.externalRenderers = [.. influences._externalRenderers];
            tile.externalLights = [.. influences._externalLights];
            tile.externalLightLinesOfSight = [.. influences._externalLightLinesOfSight];
        }
    }

    public static TileContents GetTileContents(this Vector3 point)
    {
        TileContents fallbackTileContents = null;

        foreach (var tileContents in AllTileContents)
        {
            if (tileContents.bounds.Contains(point))
                return tileContents;

            if (!tileContents.rendererBounds.Contains(point))
                continue;

            fallbackTileContents = tileContents;
        }

        return fallbackTileContents;
    }

    public static void CollectAllTilesWithinCameraFrustum(Camera camera, List<TileContents> intoList)
    {
        var frustum = camera.GetTempFrustum();

        foreach (var tileContents in AllTileContents)
        {
            if (Geometry.TestPlanesAABB(in frustum, in tileContents.bounds))
                intoList.Add(tileContents);
        }
    }
}
