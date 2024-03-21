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
    private static readonly TileContents[] TileStack = new TileContents[MaxStackCapacity];
    private static readonly int[] IndexStack = new int[MaxStackCapacity];
    private static readonly Plane[][] FrustumStack = new Plane[MaxStackCapacity][];

    private static bool warnedThatStackWasExceeded = false;

    public delegate void LineOfSightCallback(TileContents[] tileStack, Plane[][] frustumStack, int stackIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool AdvanceToNextTile(Vector3 origin, ref int stackIndex)
    {
        var tile = TileStack[stackIndex];
        var index = IndexStack[stackIndex]++;

        if (index >= tile.portals.Length)
        {
            stackIndex--;
            return false;
        }

        var portal = tile.portals[index];
        var connectedTile = portal.NextTile;

        if (connectedTile == null)
            return false;
        if (stackIndex > 0 && ReferenceEquals(connectedTile, TileStack[stackIndex - 1]))
            return false;

        if (!portal.Bounds.IntersectsFrustums(FrustumStack, stackIndex))
            return false;

        stackIndex++;
        if (stackIndex >= MaxStackCapacity)
        {
            stackIndex--;
            if (!warnedThatStackWasExceeded)
            {
                Plugin.LogWarning($"Exceeded the maximum portal occlusion culling depth of {MaxStackCapacity}.");
                warnedThatStackWasExceeded = true;
            }
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

    public static void CallForEachLineOfSight(Vector3 origin, TileContents originTile, Plane[] frustum, LineOfSightCallback callback)
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

    public static void CallForEachLineOfSight(Camera camera, TileContents originTile, LineOfSightCallback callback)
    {
        CallForEachLineOfSight(camera.transform.position, originTile, GeometryUtility.CalculateFrustumPlanes(camera), callback);
    }

    public static void CallForEachLineOfSight(Vector3 origin, TileContents originTile, LineOfSightCallback callback)
    {
        CallForEachLineOfSight(origin, originTile, [], callback);
    }

    public static void CallForEachLineOfSightToTiles(Vector3 origin, TileContents originTile, Plane[] frustum, IEnumerable<TileContents> goalTiles, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        FrustumStack[0] = frustum;
        var stackIndex = 0;

        if (goalTiles.Any(tileContents => tileContents == originTile))
            callback(TileStack, FrustumStack, stackIndex);

        while (stackIndex >= 0)
        {
            if (!AdvanceToNextTile(origin, ref stackIndex))
                continue;

            // TODO: Create a plane for portals and use that to determine whether we're
            //       moving in the right direction.

            var currentTile = TileStack[stackIndex];
            if (goalTiles.Any(tileContents => tileContents == currentTile))
                callback(TileStack, FrustumStack, stackIndex);
        }
    }

    public static void CallForEachLineOfSightToTiles(Vector3 origin, TileContents originTile, IEnumerable<TileContents> goalTiles, LineOfSightCallback callback)
    {
        CallForEachLineOfSightToTiles(origin, originTile, [], goalTiles, callback);
    }
}
