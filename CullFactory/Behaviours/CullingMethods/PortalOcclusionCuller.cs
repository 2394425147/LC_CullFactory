using System;
using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private static readonly HashSet<TileContents> VisibleTiles = [];
    private static Camera _uiCamera;

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);
        _uiCamera = Array.Find(Camera.allCameras, cam => cam.name == "UICamera");

        RenderPipelineManager.beginCameraRendering += CullForCamera;

        VisibleTiles.Clear();
    }

    private static void CullForCamera(ScriptableRenderContext context, Camera camera)
    {
        if ((camera.cullingMask & DungeonCullingInfo.TileLayerMasks) == 0 || camera == _uiCamera)
            return;

        Plugin.Log($"Culling for {camera}");

        VisibleTiles.Clear();
        VisibleTiles.FindFromCamera(camera);

        foreach (var tileContents in DungeonCullingInfo.AllTileContents)
            tileContents.SetVisible(VisibleTiles.Contains(tileContents));
    }

    private void OnDisable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(true);
        RenderPipelineManager.beginCameraRendering -= CullForCamera;
    }
}
