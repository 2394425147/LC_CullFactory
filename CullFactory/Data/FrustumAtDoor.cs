using DunGen;
using UnityEngine;

namespace CullFactory.Data;

public struct FrustumAtDoor
{
    public readonly Doorway door;
    public readonly Plane[] frustum;

    public FrustumAtDoor(Doorway door)
    {
        this.door = door;
        frustum = new Plane[4];
    }
}
