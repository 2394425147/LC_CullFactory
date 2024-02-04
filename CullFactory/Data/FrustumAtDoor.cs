using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public struct FrustumAtDoor
{
    public readonly Doorway door;
    public readonly Plane[] frustum;

    public FrustumAtDoor(Doorway door, Plane[] frustum)
    {
        this.door = door;
        this.frustum = frustum;
    }
}
