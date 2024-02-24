using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private static List<TileContents> _visibleTiles = [];
    private static List<TileContents> _visibleTilesLastCall = [];

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);

        RenderPipelineManager.beginCameraRendering += CullForCamera;

        _visibleTiles.Clear();
    }

    private static void CullForCamera(ScriptableRenderContext context, Camera camera)
    {
        if ((camera.cullingMask & DungeonCullingInfo.AllTileLayersMask) == 0)
            return;

        _visibleTiles.Clear();
        _visibleTiles.AddContentsVisibleToCamera(camera);

        foreach (var tileContent in _visibleTilesLastCall)
            tileContent.SetVisible(_visibleTiles.Contains(tileContent));

        _visibleTiles.SetVisible(true);

        (_visibleTilesLastCall, _visibleTiles) = (_visibleTiles, _visibleTilesLastCall);
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        RenderPipelineManager.beginCameraRendering -= CullForCamera;
    }
}
