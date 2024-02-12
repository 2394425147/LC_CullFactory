using System;
using System.Collections.Generic;
using System.Diagnostics;
using BepInEx;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private static List<TileContents> VisibleTiles = [];
    private static List<TileContents> VisibleTilesLastCall = [];

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);

        RenderPipelineManager.beginCameraRendering += CullForCamera;

        VisibleTiles.Clear();
    }

    private static void CullForCamera(ScriptableRenderContext context, Camera camera)
    {
        if ((camera.cullingMask & DungeonCullingInfo.TileLayerMasks) == 0)
            return;

        VisibleTiles.Clear();
        VisibleTiles.FindFromCamera(camera);

        foreach (var tileContent in VisibleTilesLastCall)
            tileContent.SetVisible(VisibleTiles.Contains(tileContent));

        VisibleTiles.SetVisible(true);

        (VisibleTilesLastCall, VisibleTiles) = (VisibleTiles, VisibleTilesLastCall);
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        RenderPipelineManager.beginCameraRendering -= CullForCamera;
    }
}
