using System.Collections.Generic;
using DunGen;
using UnityEngine;

namespace CullFactory.Data;

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