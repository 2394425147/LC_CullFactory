using System.Text;
using UnityEngine;

namespace CullFactory.Services;

public static class TransformUtility
{
    public static string GetPath(this Transform obj)
    {
        var builder = new StringBuilder(obj.name);
        var parent = obj.parent;
        while (parent != null)
        {
            builder.Insert(0, "/");
            builder.Insert(0, parent.name);
            parent = parent.parent;
        }
        return builder.ToString();
    }
}
