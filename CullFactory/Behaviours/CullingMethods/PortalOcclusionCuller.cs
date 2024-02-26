using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private static List<TileContents> _visibleTiles = [];
    private static List<TileContents> _visibleTilesLastCall = [];

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);
    }

    private void LateUpdate()
    {
        _visibleTiles.Clear();
        foreach (var camera in Camera.allCameras)
        {
            if ((camera.cullingMask & DungeonCullingInfo.AllTileLayersMask) == 0)
                continue;
            _visibleTiles.AddContentsVisibleToCamera(camera);
        }

        foreach (var tileContent in _visibleTilesLastCall)
        {
            if (!_visibleTiles.Contains(tileContent))
                tileContent.SetVisible(false);
        }

        _visibleTiles.SetVisible(true);

        (_visibleTilesLastCall, _visibleTiles) = (_visibleTiles, _visibleTilesLastCall);
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        _visibleTiles.Clear();
    }
}
