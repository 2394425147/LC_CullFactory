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
public sealed class TileContents(Tile tile, Renderer[] renderers, Light[] lights)
{
    public readonly Tile tile = tile;
    public readonly Renderer[] renderers = renderers;
    public readonly Light[] lights = lights;
}

public sealed class TileContentsBuilder(Tile tile)
{
    public readonly Tile tile = tile;
    public readonly List<Renderer> renderers = [];
    public readonly List<Light> lights = [];

    public TileContents Build()
    {
        return new TileContents(tile, [.. renderers], [.. lights]);
    }
}
