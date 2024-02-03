namespace CullFactory.Models;

internal struct Proximity
{
    public readonly float        sqrDistance;
    public readonly TileContents tileContents;

    public Proximity(float sqrDistance, TileContents tileContents)
    {
        this.sqrDistance  = sqrDistance;
        this.tileContents = tileContents;
    }
}
