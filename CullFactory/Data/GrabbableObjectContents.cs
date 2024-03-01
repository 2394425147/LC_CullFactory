using UnityEngine;

namespace CullFactory.Data;

public sealed class GrabbableObjectContents
{
    public readonly GrabbableObject item;
    public Renderer[] renderers;
    public Light[] lights;
    public Bounds localBounds;

    public GrabbableObjectContents(GrabbableObject item)
    {
        this.item = item;
        CollectContents();
    }

    public void CollectContents()
    {
        renderers = item.GetComponentsInChildren<Renderer>();
        lights = item.GetComponentsInChildren<Light>();
    }

    public bool IsWithin(Bounds bounds)
    {
        foreach (var renderer in renderers)
        {
            if (renderer == null)
                continue;
            if (!renderer.enabled)
                continue;
            if (!renderer.gameObject.activeInHierarchy)
                continue;
            if (renderer.bounds.Intersects(bounds))
                return true;
        }
        return false;
    }
}
