using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Services;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours.CullingMethods;

public sealed class PortalOcclusionCuller : CullingMethod
{
    private static readonly HashSet<TileContents> VisibleTiles = [];

    private void OnEnable()
    {
        DungeonCullingInfo.AllTileContents.SetVisible(false);

        RenderPipelineManager.beginCameraRendering += CullForCamera;

        VisibleTiles.Clear();
    }

    private static void CullForCamera(ScriptableRenderContext context, Camera camera)
    {
        if ((camera.cullingMask & DungeonCullingInfo.AllTileLayersMask) == 0)
            return;

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
