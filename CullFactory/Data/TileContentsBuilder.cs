using System.Collections.Generic;
using System.Linq;
using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public sealed class TileContentsBuilder
{
    public readonly Tile tile;
    public readonly Bounds bounds;
    public readonly HashSet<Renderer> renderers = [];
    public readonly HashSet<Light> lights = [];

    public readonly HashSet<Light> externalLights = [];
    public readonly HashSet<Renderer> externalLightOccluders = [];
    public readonly List<Plane[]> externalLightLinesOfSight = [];

    public TileContentsBuilder(Tile tile)
    {
        this.tile = tile;

        // Tile.Bounds is correct until the tile is scaled, so we have to work around some issues here.
        if (tile.OverrideAutomaticTileBounds)
        {
            // For overridden tile bounds, the sides that are set by the author will set in such a way
            // that they must have the local scale of the tile applied to be correct.
            // However, any sides that have doors will be pre-scaled, so by applying the scale we are
            // pushing them away from the origin. Therefore, we must re-condense them the same way
            // DunGen does initially.
            bounds = UnityUtil.CondenseBounds(tile.Bounds, tile.GetComponentsInChildren<Doorway>());
        }
        else
        {
            // For automatic tile bounds, all sides of the bounding box are already in pre-scaled,
            // so we just want to apply the parent transform to the bounds that have been transformed
            // into the dungeon's local space.
            bounds = tile.transform.parent.TransformBounds(tile.Placement.Bounds);
        }
    }

    public TileContents Build()
    {
        return new TileContents(tile,
                                bounds,
                                [.. renderers],
                                [.. lights],
                                [.. externalLights],
                                [.. externalLightOccluders],
                                [.. externalLightLinesOfSight]);
    }
}
