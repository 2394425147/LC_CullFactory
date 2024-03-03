using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CullFactory.Services;

public static class StringUtility
{
    public static IEnumerable<string> SplitByComma(this string input)
    {
        return input.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(name => name.Trim().Trim('"'));
    }

    public static string JoinByComma(this IEnumerable<string> input)
    {
        return string.Join(", ", input);
    }

    public static string GetPath(this UnityEngine.Transform obj)
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
