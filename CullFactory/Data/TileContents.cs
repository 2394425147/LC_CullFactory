using DunGen;
using System.Collections.Generic;
using UnityEngine;

namespace CullFactory.Data;

/// <summary>
///     Stores the contents of a DunGen Tile relevant to culling it from the view.
///     Note that none of the fields in this class are guaranteed to be children of
///     the Tile they are associated with. Children of doors are shared between the
///     both neighboring Tiles.
/// </summary>
public sealed class TileContents(
    Tile tile,
    Renderer[] renderers,
    Light[] lights,
    Light[] externalLights,
    Renderer[] externalLightOccluders)
{
    public readonly Tile tile = tile;
    public readonly Bounds bounds = tile.Bounds;
    public readonly Renderer[] renderers = renderers;
    public readonly Light[] lights = lights;

    public readonly Light[] externalLights = externalLights;
    public readonly Renderer[] externalLightOccluders = externalLightOccluders;

    private bool _visible = true;

    public void SetVisible(bool visible)
    {
        if (visible == _visible)
            return;

        foreach (var renderer in renderers)
            renderer.forceRenderingOff = !visible;
        foreach (var light in lights)
            light.enabled = visible;

        foreach (var light in externalLights)
            light.enabled = visible;
        foreach (var renderer in externalLightOccluders)
            renderer.forceRenderingOff = !visible;

        _visible = visible;

        Plugin.Log($"{(visible ? "Showing" : "Culling")} {tile.name}");
    }
}

public sealed class TileContentsBuilder(Tile tile)
{
    public readonly Tile tile = tile;
    public readonly HashSet<Renderer> renderers = [];
    public readonly HashSet<Light> lights = [];

    public readonly HashSet<Light> externalLights = [];
    public readonly HashSet<Renderer> externalLightOccluders = [];

    public TileContents Build()
    {
        return new TileContents(tile, [.. renderers], [.. lights], [.. externalLights], [.. externalLightOccluders]);
    }
}
