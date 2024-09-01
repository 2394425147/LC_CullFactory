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

    public static string HumanReadableList(this IEnumerable<string> input)
    {
        var enumerator = input.GetEnumerator();
        if (!enumerator.MoveNext())
            return "";

        var builder = new StringBuilder(enumerator.Current);
        if (!enumerator.MoveNext())
            return builder.ToString();

        while (true)
        {
            var current = enumerator.Current;

            if (enumerator.MoveNext())
            {
                builder.Append(", ");
                builder.Append(current);
            }
            else
            {
                builder.Append(", and ");
                builder.Append(current);
                break;
            }
        }

        return builder.ToString();
    }
}
