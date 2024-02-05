using CullFactory.Data;
using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Behaviours;
  
public sealed class PortalOcclusionCuller : MonoBehaviour
{
    public static PortalOcclusionCuller Instance;

    private readonly List<TileContents> _visibleTiles = new();

    private void Awake()
    {
        Instance = this;

        HideTileContents(DungeonCullingInfo.AllTileContents.Values);
    }

    private static void HideTileContents(IEnumerable<TileContents> tileContents)
    {
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
            foreach (var renderer in tile.renderers)
                renderer.forceRenderingOff = false;
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

    private void OnDestroy()
    {
        ShowTileContents(DungeonCullingInfo.AllTileContents.Values);
    }
}
