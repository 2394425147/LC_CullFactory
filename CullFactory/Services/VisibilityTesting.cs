using CullFactory.Data;
using DunGen;
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

    private const int MaxStackCapacity = 15;
    private static readonly Tile[] TileStack = new Tile[MaxStackCapacity];
    private static readonly int[] IndexStack = new int[MaxStackCapacity];
    private static readonly Plane[][] FrustumStack = new Plane[MaxStackCapacity][];

    public delegate void LineOfSightCallback(Tile[] tileStack, Plane[][] frustumStack, int stackIndex);

    public static void CallForEachLineOfSight(Vector3 origin, Tile originTile, Plane[] frustum, LineOfSightCallback callback)
    {
        TileStack[0] = originTile;
        IndexStack[0] = 0;
        FrustumStack[0] = frustum;
        var stackIndex = 0;

        callback(TileStack, FrustumStack, stackIndex);

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

            var portal = DungeonCullingInfo.AllPortals[doorway];

            if (!portal.Bounds.IntersectsFrustums(FrustumStack, stackIndex))
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
}
