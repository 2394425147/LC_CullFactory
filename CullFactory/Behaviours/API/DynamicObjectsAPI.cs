using CullFactory.Data;
using UnityEngine;

namespace CullFactory.Behaviours.API;

public static class DynamicObjectsAPI
{
    // Moves the item into a pool of inside or outside items to be culled based on the position of
    // the cameras. If an item is moved inside or outside the interior and is then invisible, this
    // will allow CullFactory to become aware of the change in position and make it visible again.
    public static void RefreshGrabbableObjectPosition(GrabbableObject item)
    {
        DynamicObjects.MarkGrabbableObjectDirty(item);
    }

    // Moves the light into a pool of inside or outside lights to be culled based on the position of
    // the cameras. If a light is moved inside or outside the interior and is then invisible, this
    // will allow CullFactory to become aware of the change in position and make it visible again.
    public static void RefreshLightPosition(Light light)
    {
        DynamicObjects.RefreshSpecificLight(light);
    }
}
