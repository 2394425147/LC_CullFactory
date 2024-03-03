using System.Collections.Generic;
using System.Linq;
using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public sealed class TileContentsBuilder(Tile tile)
{
    public readonly Tile tile = tile;
    public readonly Bounds bounds = tile.OverrideAutomaticTileBounds
                                            ? tile.Bounds
                                            : tile.transform.parent.TransformBounds(tile.Placement.Bounds);
    public readonly HashSet<Renderer> renderers = [];
    public readonly HashSet<Light> lights = [];

    public readonly HashSet<Light> externalLights = [];
    public readonly HashSet<Renderer> externalLightOccluders = [];

    public TileContents Build()
    {
        return new TileContents(tile,
                                bounds,
                                [.. renderers],
                                [.. lights],
                                [.. externalLights],
                                [.. externalLightOccluders]);
    }
}
