using DunGen;
using UnityEngine;

namespace CullFactory.Models;

public sealed class TileContents
{
    public readonly Tile       tile;
    public readonly Renderer[] renderers;
    public readonly Light[]    lights;

    public static TileContents FromTile(Tile tile) => new(tile);

    private TileContents(Tile tile)
    {
        this.tile = tile;

        renderers = tile.GetComponentsInChildren<Renderer>();
        lights    = tile.GetComponentsInChildren<Light>();
    }
}
