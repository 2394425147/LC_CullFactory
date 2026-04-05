using System.Text;
using UnityEngine;

namespace CullFactory.Services;

public static class TransformUtility
{
    public static string GetPath(this Transform obj, Transform stopAt = null)
    {
        var builder = new StringBuilder(obj.name);
        var parent = obj.parent;
        while (parent != stopAt)
        {
            builder.Insert(0, "/");
            builder.Insert(0, parent.name);
            parent = parent.parent;
        }
        return builder.ToString();
    }

    public static bool TryGetComponentInParent<T>(this Transform transform, out T component)
    {
        component = transform.GetComponentInParent<T>();
        return component != null;
    }
}
