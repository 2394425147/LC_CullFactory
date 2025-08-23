using CullFactory.Data;
using CullFactoryBurst;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace CullFactory.Services;

public static class VisibilityTesting
{
    private const int MaxStackCapacity = 16;
    private static readonly TileContents[] TileStack = new TileContents[MaxStackCapacity];
    private static readonly int[] IndexStack = new int[MaxStackCapacity];
    private static readonly NativeSlice<Plane>[] Frustums = new NativeSlice<Plane>[MaxStackCapacity];
    private static readonly NativeArray<Plane> FrustumPlanes = new(6 * MaxStackCapacity, Allocator.Persistent);

    private static bool warnedThatStackWasExceeded = false;

    public delegate void LineOfSightCallback(TileContents[] tileStack, NativeSlice<Plane>[] frustums, int stackIndex);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe void DropTopStackFrame(ref int stackIndex, ref int frustumPlanesCount)
    {
        frustumPlanesCount -= Frustums[stackIndex].Length;
        Frustums[stackIndex] = default;
        stackIndex--;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe bool AdvanceToNextTile(Vector3 origin, ref int stackIndex, ref int frustumPlanesCount)
    {
        var tile = TileStack[stackIndex];
        var index = IndexStack[stackIndex]++;

        if (index >= tile.portals.Length)
        {
            DropTopStackFrame(ref stackIndex, ref frustumPlanesCount);
            return false;
        }

        var portal = tile.portals[index];
        var connectedTile = portal.NextTile;

        if (connectedTile == null)
            return false;
        if (stackIndex > 0 && ReferenceEquals(connectedTile, TileStack[stackIndex - 1]))
            return false;

        var allPlanes = FrustumPlanes.Slice(0, frustumPlanesCount);
        if (!Geometry.TestPlanesAABB(in allPlanes, in portal.Bounds))
            return false;

        stackIndex++;
        if (stackIndex >= MaxStackCapacity)
        {
            DropTopStackFrame(ref stackIndex, ref frustumPlanesCount);
            if (!warnedThatStackWasExceeded)
            {
                Plugin.LogWarning($"Exceeded the maximum portal occlusion culling depth of {MaxStackCapacity}.");
                warnedThatStackWasExceeded = true;
            }
            return false;
        }

        TileStack[stackIndex] = connectedTile;
        IndexStack[stackIndex] = 0;
        var frustumSize = portal.GetFrustumPlanes(in origin, FrustumPlanes.Slice(frustumPlanesCount).GetPtr());
        Frustums[stackIndex] = FrustumPlanes.Slice(frustumPlanesCount, frustumSize);
        frustumPlanesCount += frustumSize;

        return true;
    }

    public static void CallForEachLineOfSight(Vector3 origin, TileContents originTile, NativeSlice<Plane> frustum, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        frustum.CopyTo(FrustumPlanes);
        Frustums[0] = FrustumPlanes.Slice(0, frustum.Length);
        var stackIndex = 0;
        var frustumPlanesCount = frustum.Length;

        callback(TileStack, Frustums, stackIndex);

        while (stackIndex >= 0)
        {
            if (!AdvanceToNextTile(origin, ref stackIndex, ref frustumPlanesCount))
                continue;

            callback(TileStack, Frustums, stackIndex);
        }
    }

    public static void CallForEachLineOfSight(Camera camera, TileContents originTile, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        Frustums[0] = FrustumPlanes.Slice(0, 6);
        camera.ExtractFrustumPlanes(FrustumPlanes);
        var stackIndex = 0;
        var frustumPlanesCount = 6;

        callback(TileStack, Frustums, stackIndex);

        var origin = camera.transform.position;

        while (stackIndex >= 0)
        {
            if (!AdvanceToNextTile(origin, ref stackIndex, ref frustumPlanesCount))
                continue;

            callback(TileStack, Frustums, stackIndex);
        }
        CallForEachLineOfSight(camera.transform.position, originTile, camera.GetTempFrustum(), callback);
    }

    public static void CallForEachLineOfSight(Vector3 origin, TileContents originTile, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        Frustums[0] = default;
        var stackIndex = 0;
        var frustumPlanesCount = 0;

        callback(TileStack, Frustums, stackIndex);

        while (stackIndex >= 0)
        {
            if (!AdvanceToNextTile(origin, ref stackIndex, ref frustumPlanesCount))
                continue;

            callback(TileStack, Frustums, stackIndex);
        }
    }

    private static bool PlanesIntersectAnyTile(in NativeSlice<Plane> planes, HashSet<TileContents> tiles)
    {
        foreach (var tile in tiles)
        {
            if (Geometry.TestPlanesAABB(in planes, tile.bounds))
                return true;
        }

        return false;
    }

    public static bool CallForEachLineOfSightTowardTiles(Vector3 origin, TileContents originTile, NativeSlice<Plane> frustum, HashSet<TileContents> goalTiles, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        frustum.CopyTo(FrustumPlanes);
        Frustums[0] = FrustumPlanes.Slice(0, frustum.Length);
        var stackIndex = 0;
        var frustumPlanesCount = frustum.Length;

        callback(TileStack, Frustums, stackIndex);

        var reachedGoal = goalTiles.Contains(originTile);

        while (stackIndex >= 0)
        {
            if (!AdvanceToNextTile(origin, ref stackIndex, ref frustumPlanesCount))
                continue;

            if (!PlanesIntersectAnyTile(Frustums[stackIndex], goalTiles))
            {
                DropTopStackFrame(ref stackIndex, ref frustumPlanesCount);
                continue;
            }

            if (goalTiles.Contains(TileStack[stackIndex]))
                reachedGoal = true;
            
            callback(TileStack, Frustums, stackIndex);
        }

        return reachedGoal;
    }

    public static bool CallForEachLineOfSightTowardTiles(Vector3 origin, TileContents originTile, HashSet<TileContents> goalTiles, LineOfSightCallback callback)
    {
        return CallForEachLineOfSightTowardTiles(origin, originTile, [], goalTiles, callback);
    }
}
