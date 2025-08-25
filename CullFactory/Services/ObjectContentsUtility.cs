using System.Collections.Generic;
using CullFactory.Data;
using CullFactoryBurst;
using UnityEngine;

namespace CullFactory.Services;

public static class ObjectContentsUtility
{
    public static void SetSelfVisible(this IEnumerable<TileContents> tiles, bool visible)
    {
        foreach (var tile in tiles)
            tile.SetSelfVisible(visible);
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
            VisibilityTesting.CallForEachLineOfSight(camera, currentTileContents,
                                                     (tiles, frustums, index) => result.Add(tiles[index]));
    }

    public static void AddContentsWithinCameraFrustum(this ICollection<TileContents> result, Camera camera)
    {
        var frustum = camera.GetTempFrustum();

        for (var i = 0; i < DungeonCullingInfo.AllDungeonData.Length; i++)
        {
            ref var dungeonData = ref DungeonCullingInfo.AllDungeonData[i];
            if (!dungeonData.IsValid)
                continue;
            if (!Geometry.TestPlanesAABB(in frustum, in dungeonData.Bounds))
                continue;

            foreach (var tileContents in dungeonData.AllTileContents)
            {
                if (!Geometry.TestPlanesAABB(in frustum, in tileContents.bounds))
                    continue;
                result.Add(tileContents);
            }
        }
    }
}
