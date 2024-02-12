using System;
using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private static readonly List<TileContents> VisibleTiles = [];

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

        Plugin.Log($"Culling for {camera}");

        VisibleTiles.SetVisible(false);
        VisibleTiles.Clear();

        VisibleTiles.FindFromCamera(camera);
        VisibleTiles.SetVisible(true);
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        RenderPipelineManager.beginCameraRendering -= CullForCamera;
    }
}
