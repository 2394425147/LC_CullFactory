using System.Collections.Generic;
using CullFactory.Data;
using UnityEngine;

namespace CullFactory.Services;

public static class TileContentsUtility
{
    public static void SetVisible(this IEnumerable<TileContents> tiles, bool visible)
    {
        foreach (var tile in tiles)
            tile.SetVisible(visible);
    }

    public static void FindFromCamera(this ICollection<TileContents> result, Camera camera)
    {
        if (camera.orthographic)
        {
            result.FindFromCameraOrthographic(camera);
            return;
        }

        var currentTileContents = camera.transform.position.GetTileContents();

        if (currentTileContents != null)
            DungeonCullingInfo.CallForEachLineOfSight(camera, currentTileContents.tile,
                                                      (tiles, index) =>
                                                          result.Add(DungeonCullingInfo.TileContentsForTile[tiles[index]]));
    }

    public static void FindFromCameraOrthographic(this ICollection<TileContents> result, Camera camera)
    {
        var frustum = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var tileContents in DungeonCullingInfo.AllTileContents)
        {
            if (GeometryUtility.TestPlanesAABB(frustum, tileContents.bounds))
                result.Add(tileContents);
        }
    }
}
