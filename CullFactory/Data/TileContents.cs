using System;
using DunGen;
using UnityEngine;

namespace CullFactory.Data;

[Serializable]
public class TileContents
{
    private readonly Light[] _lights;

    private readonly Renderer[] _renderers;
    public readonly Tile tile;

    private bool _enabled = true;

    public TileContents(in Tile tile)
    {
        this.tile = tile;

        _renderers = tile.GetComponentsInChildren<Renderer>();
        _lights = tile.GetComponentsInChildren<Light>();

        Plugin.Log($"Found tile {tile.name} with {_renderers.Length} mesh renderers and {_lights.Length} lights");
    }

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
}
