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

    private void Update()
    {
        HideTileContents(_visibleTiles);
        _visibleTiles.Clear();

        var camera = StartOfRound.Instance.activeCamera;
        var currentTile = camera.transform.position.GetTile();

        if (currentTile != null)
        {
            DungeonCullingInfo.CallForEachLineOfSight(camera, currentTile, (tiles, index) => _visibleTiles.Add(DungeonCullingInfo.AllTileContents[tiles[index]]));

            ShowTileContents(_visibleTiles);
        }
    }

    private void OnDisable()
    {
        ShowTileContents(DungeonCullingInfo.AllTileContents.Values);
    }
}
