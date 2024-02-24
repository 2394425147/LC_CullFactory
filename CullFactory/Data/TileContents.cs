using DunGen;
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

    public void SetVisible(bool visible)
    {
        foreach (var renderer in renderers)
            renderer.forceRenderingOff = !visible;
        foreach (var light in lights)
            light.enabled = visible;

        foreach (var light in externalLights)
            light.enabled = visible;
        foreach (var renderer in externalLightOccluders)
            renderer.forceRenderingOff = !visible;
    }
}
