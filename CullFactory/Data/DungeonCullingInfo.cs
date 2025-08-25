using System;
using System.Collections.Generic;
using System.Linq;
using CullFactory.Behaviours.CullingMethods;
using CullFactory.Services;
using CullFactoryBurst;
using DunGen;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace CullFactory.Data;

internal static class DungeonCullingInfo
{
    private const int RendererIntrusionTileDepth = 2;
    private const float RendererIntrusionDistance = 0.01f;

    internal struct DungeonData(Dungeon dungeon)
    {
        public WeakReference<Dungeon> DungeonRef = new(dungeon);
        public readonly bool IsValid => DungeonRef.TryGetTarget(out var dungeon) && dungeon != null;

        public Bounds Bounds;

        public TileContents[] AllTileContents { get; internal set; }
        public Dictionary<Tile, TileContents> TileContentsForTile { get; internal set; }

        public Light[] AllLightsInDungeon { get; internal set; }
    }

    public static DungeonData[] AllDungeonData = [];

    public static void OnDungeonGenerated(Dungeon dungeon)
    {
        var dungeonData = new DungeonData(dungeon);

        var flow = dungeon.DungeonFlow;
        if (flow == null)
        {
            Plugin.LogError($"Dungeon flow for {dungeon} was null!");
            return;
        }
        var interiorName = flow.name;
        if (interiorName != null && !Config.ShouldEnableCullingForInterior(interiorName))
            return;

        var derivePortalBoundsFromTile = Array.IndexOf(Config.InteriorsWithFallbackPortals, interiorName) != -1;
        if (derivePortalBoundsFromTile)
            Plugin.LogAlways($"Using tile bounds to determine the size of portals for {interiorName}.");

        var startTime = Time.realtimeSinceStartup;
        CollectAllTileContents(dungeon, derivePortalBoundsFromTile, ref dungeonData);
        Plugin.Log($"Preparing tile information for {interiorName} took {(Time.realtimeSinceStartup - startTime) * 1000:0.###}ms");

        AllDungeonData = [.. AllDungeonData, dungeonData];

        CullingMethod.Initialize();
    }

    public static void RefreshCullingInfo()
    {
        Plugin.LogAlways($"Refreshing culling info");
        AllDungeonData = [];

        foreach (var dungeon in UnityEngine.Object.FindObjectsOfType<Dungeon>())
        {
            Plugin.LogAlways($"Dungeon found: {dungeon}");
            OnDungeonGenerated(dungeon);
        }
    }

    public static void CleanUpDestroyedDungeons()
    {
        var newDungeonData = new List<DungeonData>(AllDungeonData);
        for (var i = AllDungeonData.Length - 1; i >= 0; i--)
        {
            if (!AllDungeonData[i].IsValid)
                newDungeonData.RemoveAt(i);
        }
        AllDungeonData = [.. newDungeonData];
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

    private static void CollectAllTileContents(Dungeon dungeon, bool derivePortalBoundsFromTile, ref DungeonData data)
    {
        FillTileContentsCollections(dungeon, ref data);

        CreateAndAssignPortals(derivePortalBoundsFromTile, ref data);

        AddIntrudingRenderersToTileContents(ref data);

        AddLightInfluencesToTileContents(ref data);
    }

    private static void FillTileContentsCollections(Dungeon dungeon, ref DungeonData data)
    {
        var tiles = dungeon.AllTiles;
        data.AllTileContents = new TileContents[tiles.Count];
        data.TileContentsForTile = new Dictionary<Tile, TileContents>(data.AllTileContents.Length);

        var lightsInDungeon = new List<Light>();

        var dungeonMin = Vector3.positiveInfinity;
        var dungeonMax = Vector3.negativeInfinity;

        var i = 0;
        foreach (var tile in tiles)
        {
            var tileContents = new TileContents(tile);
            data.AllTileContents[i++] = tileContents;
            data.TileContentsForTile[tile] = tileContents;
            lightsInDungeon.AddRange(tileContents.lights);

            dungeonMin = Vector3.Min(dungeonMin, tileContents.rendererBounds.min);
            dungeonMax = Vector3.Max(dungeonMax, tileContents.rendererBounds.max);
        }

        data.AllLightsInDungeon = [.. lightsInDungeon];
        data.Bounds.min = dungeonMin;
        data.Bounds.max = dungeonMax;
    }

    private static void CreateAndAssignPortals(bool derivePortalBoundsFromTile, ref DungeonData data)
    {
        foreach (var tileContents in data.TileContentsForTile.Values)
        {
            var doorways = tileContents.tile.UsedDoorways;
            var doorwayCount = doorways.Count;
            var portals = new List<Portal>(doorwayCount);
            for (var j = 0; j < doorwayCount; j++)
            {
                var doorway = doorways[j];
                if (doorway.ConnectedDoorway == null)
                    continue;
                portals.Add(new Portal(doorway, derivePortalBoundsFromTile, data.TileContentsForTile[doorway.ConnectedDoorway.Tile]));
            }
            tileContents.portals = [.. portals];
        }
    }

    private static void AddIntrudingRenderersToTileContents(ref DungeonData data)
    {
        for (var i = 0; i < data.AllTileContents.Length; i++)
        {
            var tile = data.AllTileContents[i];
            tile.renderers = [.. tile.renderers, .. GetIntrudingRenderers(data.AllTileContents[i])];
        }
    }

    private readonly struct LightInfluenceCollections(TileContents tileContents)
    {
        internal readonly HashSet<Renderer> _externalRenderers = new(tileContents.externalRenderers);
        internal readonly List<Light> _externalLights = new(tileContents.externalLights);
        internal readonly List<Plane[]> _externalLightLinesOfSight = new(tileContents.externalLightLinesOfSight);
    }

    private static void AddLightInfluencesToTileContents(ref DungeonData data)
    {
        var lightInfluenceCollectionLookup = new Dictionary<TileContents, LightInfluenceCollections>(data.AllTileContents.Length);

        foreach (var tile in data.AllTileContents)
            lightInfluenceCollectionLookup[tile] = new(tile);

        foreach (var (tile, light) in
                 data.AllTileContents.SelectMany(tile => tile.lights.Select(light => (tile, light))))
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
                foreach (var otherTile in data.AllTileContents)
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

        foreach (var tile in data.AllTileContents)
        {
            var influences = lightInfluenceCollectionLookup[tile];
            tile.externalRenderers = [.. influences._externalRenderers];
            tile.externalLights = [.. influences._externalLights];
            tile.externalLightLinesOfSight = [.. influences._externalLightLinesOfSight];
        }
    }

    public static bool PointIsInAnyInterior(Vector3 point)
    {
        for (var i = 0; i < AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref AllDungeonData[i];
            if (!dungeonData.IsValid)
                continue;
            if (!dungeonData.Bounds.Contains(point))
                continue;

            foreach (var tileContents in dungeonData.AllTileContents)
            {
                if (tileContents.rendererBounds.Contains(point))
                    return true;
            }
        }

        return false;
    }

    public static TileContents GetTileContents(this Vector3 point)
    {
        TileContents fallbackTileContents = null;

        for (var i = 0; i < AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref AllDungeonData[i];
            if (!dungeonData.IsValid)
                continue;
            if (!dungeonData.Bounds.Contains(point))
                continue;

            foreach (var tileContents in dungeonData.AllTileContents)
            {
                if (tileContents.bounds.Contains(point))
                    return tileContents;

                if (!tileContents.rendererBounds.Contains(point))
                    continue;

                fallbackTileContents = tileContents;
            }
        }

        return fallbackTileContents;
    }

    public static void CollectAllTilesWithinCameraFrustum(Camera camera, List<TileContents> intoList)
    {
        var frustum = camera.GetTempFrustum();

        for (var i = 0; i < AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref AllDungeonData[i];
            if (!dungeonData.IsValid)
                continue;

            foreach (var tileContents in dungeonData.AllTileContents)
            {
                if (Geometry.TestPlanesAABB(in frustum, in tileContents.bounds))
                    intoList.Add(tileContents);
            }
        }
    }

    public static bool TryGetTileContentsForTile(Tile tile, out TileContents tileContents)
    {
        for (var i = 0; i < AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref AllDungeonData[i];
            if (!dungeonData.IsValid)
                continue;

            if (dungeonData.TileContentsForTile.TryGetValue(tile, out tileContents))
                return true;
        }

        tileContents = null;
        return false;
    }

    public static void SetAllTileContentsVisible(bool visible)
    {
        for (var i = 0; i < AllDungeonData.Length; i++)
            AllDungeonData[i].AllTileContents.SetSelfVisible(visible);
    }
}
