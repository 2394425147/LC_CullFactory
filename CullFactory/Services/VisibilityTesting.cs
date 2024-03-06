using CullFactory.Data;
using DunGen;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace CullFactory.Services;

public static class VisibilityTesting
{
    internal static bool IntersectsFrustums(this Bounds bounds, Plane[][] frustums, int lastFrustum)
    {
        for (var i = 0; i <= lastFrustum; i++)
        {
            if (!GeometryUtility.TestPlanesAABB(frustums[i], bounds))
                return false;
        }
        return true;
    }

    private const int MaxStackCapacity = 16;
    private static readonly Tile[] TileStack = new Tile[MaxStackCapacity];
    private static readonly int[] IndexStack = new int[MaxStackCapacity];
    private static readonly Plane[][] FrustumStack = new Plane[MaxStackCapacity][];

    public delegate void LineOfSightCallback(Tile[] tileStack, Plane[][] frustumStack, int stackIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AdvanceToNextTile(Vector3 origin, ref int stackIndex)
    {
        var tile = TileStack[stackIndex];
        var index = IndexStack[stackIndex]++;

        if (index >= tile.UsedDoorways.Count)
        {
            stackIndex--;
            return false;
        }

        var doorway = tile.UsedDoorways[index];
        var connectedTile = doorway.ConnectedDoorway?.Tile;

        if (connectedTile == null)
            return false;
        if (stackIndex > 0 && ReferenceEquals(connectedTile, TileStack[stackIndex - 1]))
            return false;

        var portal = DungeonCullingInfo.AllPortals[doorway];

        if (!portal.Bounds.IntersectsFrustums(FrustumStack, stackIndex))
            return false;

        stackIndex++;
        if (stackIndex >= MaxStackCapacity)
        {
            stackIndex--;
            Plugin.LogError($"Exceeded the maximum portal occlusion culling depth of {MaxStackCapacity} at {stackIndex}");
            return false;
        }

        TileStack[stackIndex] = connectedTile;
        IndexStack[stackIndex] = 0;

        if (FrustumStack[stackIndex] is null)
            FrustumStack[stackIndex] = portal.GetFrustumPlanes(origin);
        else
            portal.GetFrustumPlanesNonAlloc(origin, FrustumStack[stackIndex]);

        return true;
    }

    public static void CallForEachLineOfSight(Vector3 origin, Tile originTile, Plane[] frustum, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        FrustumStack[0] = frustum;
        var stackIndex = 0;

        callback(TileStack, FrustumStack, stackIndex);

        while (stackIndex >= 0)
        {
            if (!AdvanceToNextTile(origin, ref stackIndex))
                continue;

            callback(TileStack, FrustumStack, stackIndex);
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

    public static void CallForEachLineOfSightToTiles(Vector3 origin, Tile originTile, Plane[] frustum, IEnumerable<TileContents> goalTiles, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        FrustumStack[0] = frustum;
        var stackIndex = 0;

        if (goalTiles.Any(tileContents => tileContents.tile == originTile))
            callback(TileStack, FrustumStack, stackIndex);

        while (stackIndex >= 0)
        {
            if (!AdvanceToNextTile(origin, ref stackIndex))
                continue;

            var frustumContainsGoalTile = false;
            foreach (var goalTile in goalTiles)
            {
                if (GeometryUtility.TestPlanesAABB(FrustumStack[stackIndex], goalTile.bounds))
                {
                    frustumContainsGoalTile = true;
                    break;
                }
            }
            if (!frustumContainsGoalTile)
            {
                stackIndex--;
                continue;
            }

            var currentTile = TileStack[stackIndex];
            if (goalTiles.Any(tileContents => tileContents.tile == currentTile))
                callback(TileStack, FrustumStack, stackIndex);
        }
    }

    public static void CallForEachLineOfSightToTiles(Vector3 origin, Tile originTile, IEnumerable<TileContents> goalTiles, LineOfSightCallback callback)
    {
        CallForEachLineOfSightToTiles(origin, originTile, [], goalTiles, callback);
    }
}
