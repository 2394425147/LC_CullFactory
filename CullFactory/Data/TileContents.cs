using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public sealed class TileContents
{
    public readonly Tile tile;
    public readonly Renderer[] renderers;
    public readonly Light[] lights;

    public TileContents(Tile tile)
    {
        this.tile = tile;

        renderers = tile.GetComponentsInChildren<Renderer>();
        lights = tile.GetComponentsInChildren<Light>();
    }
}
