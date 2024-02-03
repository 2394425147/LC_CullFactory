using DunGen;
using UnityEngine;

namespace CullFactory.Models;

public sealed class TileContents
{
    public readonly Tile tile;

    private readonly Renderer[] _renderers;
    private readonly Light[]    _lights;

    private bool _enabled = true;

    public static TileContents FromTile(in Tile tile) => new(tile);

    public void SetActive(in bool value)
    {
        if (_enabled == value)
            return;

        _enabled = value;

        foreach (var renderer in _renderers)
            renderer.forceRenderingOff = !value;

        foreach (var light in _lights)
            light.enabled = value;
    }

    private TileContents(in Tile tile)
    {
        this.tile = tile;

        _renderers = tile.GetComponentsInChildren<Renderer>();
        _lights    = tile.GetComponentsInChildren<Light>();

        Plugin.Log($"Found tile {tile.name} with {_renderers.Length} mesh renderers and {_lights.Length} lights");
    }
}
