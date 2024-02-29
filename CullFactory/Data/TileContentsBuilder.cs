using System.Collections.Generic;
using System.Linq;
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
        return new TileContents(tile,
                                [.. renderers],
                                [.. lights], [.. lights.Select(light => light.cullingMask)],
                                [.. externalLights], [.. externalLights.Select(light => light.cullingMask)],
                                [.. externalLightOccluders]);
    }
}
