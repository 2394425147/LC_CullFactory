using System;
using DunGen;
using UnityEngine;

namespace CullFactory.Models;

public class TileVisibility
{
    public readonly Tile parentTile;

    private readonly MeshRenderer[] _meshRenderers;
    private readonly Light[]        _lights;

    private bool _previouslyVisible = true;

    public TileVisibility(Tile parentTile)
    {
        this.parentTile = parentTile;

        _meshRenderers = Array.FindAll(parentTile.GetComponentsInChildren<MeshRenderer>(), renderer => renderer.enabled);
        _lights        = Array.FindAll(parentTile.GetComponentsInChildren<Light>(),        renderer => renderer.enabled);

        Plugin.Log($"Found tile {parentTile.name} with {_meshRenderers.Length} mesh renderers and {_lights.Length} lights");
    }

    public void SetVisible(bool value)
    {
        if (_previouslyVisible == value)
            return;

        Plugin.Log(value ? $"Showing {parentTile.name}" : $"Culling {parentTile.name}");

        foreach (var meshRenderer in _meshRenderers)
            meshRenderer.enabled = value;

        foreach (var light in _lights)
            light.enabled = value;

        _previouslyVisible = value;
    }
}
