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
        HideTileContents(DungeonCullingInfo.AllTileContents.Values);

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
            var frustum = GeometryUtility.CalculateFrustumPlanes(fromCamera);

            foreach (var tilePair in DungeonCullingInfo.AllTileContents)
            {
                if (GeometryUtility.TestPlanesAABB(frustum, tilePair.Key.Bounds))
                    intoList.Add(tilePair.Value);
            }
            return;
        }

        var currentTile = fromCamera.transform.position.GetTile();

        if (currentTile != null)
            DungeonCullingInfo.CallForEachLineOfSight(fromCamera, currentTile, (tiles, index) => intoList.Add(DungeonCullingInfo.AllTileContents[tiles[index]]));
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
        ShowTileContents(DungeonCullingInfo.AllTileContents.Values);

        RenderPipelineManager.beginCameraRendering -= CullForCamera;
    }
}
