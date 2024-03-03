using System.Collections.Generic;
using CullFactory.Data;
using UnityEngine;

namespace CullFactory.Services;

public static class ObjectContentsUtility
{
    public static void SetVisible(this IEnumerable<TileContents> tiles, bool visible)
    {
        foreach (var tile in tiles)
            tile.SetVisible(visible);
    }

    public static void SetVisible(this IEnumerable<GrabbableObjectContents> items, bool visible)
    {
        foreach (var item in items)
            item.SetVisible(visible);
    }

    public static void AddContentsVisibleToCamera(this ICollection<TileContents> result, Camera camera)
    {
        if (camera.orthographic)
        {
            result.AddContentsWithinCameraFrustum(camera);
            return;
        }

        var currentTileContents = camera.transform.position.GetTileContents();

        if (currentTileContents != null)
            VisibilityTesting.CallForEachLineOfSight(camera, currentTileContents.tile,
                                                     (tiles, frustums, index) =>
                                                         result.Add(DungeonCullingInfo.TileContentsForTile[tiles[index]]));
    }

    public static void AddContentsWithinCameraFrustum(this ICollection<TileContents> result, Camera camera)
    {
        var frustum = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var tileContents in DungeonCullingInfo.AllTileContents)
        {
            if (GeometryUtility.TestPlanesAABB(frustum, tileContents.bounds))
                result.Add(tileContents);
        }
    }
}
