using CullFactory.Data;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours;

public sealed class PortalOcclusionCuller : MonoBehaviour
{
    private readonly List<TileContents> _visibleTiles = new();

    private void OnEnable()
    {
        HideTileContents(DungeonCullingInfo.AllTileContents);

        RenderPipelineManager.beginCameraRendering += CullForCamera;
    }

    private static void HideTileContents(IEnumerable<TileContents> tileContents)
    {
        foreach (var tile in tileContents)
        {
            foreach (var light in tile.externalLights)
                light.enabled = false;
            foreach (var renderer in tile.externalLightOccluders)
                renderer.forceRenderingOff = true;
        }

        foreach (var tile in tileContents)
        {
            foreach (var renderer in tile.renderers)
                renderer.forceRenderingOff = true;
            foreach (var light in tile.lights)
                light.enabled = false;
        }
    }

    private static void ShowTileContents(IEnumerable<TileContents> tileContents)
    {
        foreach (var tile in tileContents)
        {
            foreach (var light in tile.externalLights)
                light.enabled = true;
            foreach (var renderer in tile.externalLightOccluders)
            {
                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }
        }
        foreach (var tile in tileContents)
        {
            foreach (var renderer in tile.renderers)
            {
                renderer.forceRenderingOff = false;
                renderer.shadowCastingMode = ShadowCastingMode.On;
            }
            foreach (var light in tile.lights)
                light.enabled = true;
        }
    }

    private void CollectVisibleTiles(Camera fromCamera, List<TileContents> intoList)
    {
        if (fromCamera.orthographic)
        {
            DungeonCullingInfo.CollectAllTilesWithinCameraFrustum(fromCamera, intoList);
            return;
        }

        var currentTileContents = fromCamera.transform.position.GetTileContents();

        if (currentTileContents != null)
            DungeonCullingInfo.CallForEachLineOfSight(fromCamera, currentTileContents.tile, (tiles, index) => intoList.Add(DungeonCullingInfo.TileContentsForTile[tiles[index]]));
    }

    private void CullForCamera(ScriptableRenderContext context, Camera camera)
    {
        if ((camera.cullingMask & DungeonCullingInfo.AllTileLayersMask) == 0)
            return;

        HideTileContents(_visibleTiles);
        _visibleTiles.Clear();

        CollectVisibleTiles(camera, _visibleTiles);
        ShowTileContents(_visibleTiles);
    }

    private void OnDisable()
    {
        ShowTileContents(DungeonCullingInfo.AllTileContents);

        RenderPipelineManager.beginCameraRendering -= CullForCamera;
    }
}
