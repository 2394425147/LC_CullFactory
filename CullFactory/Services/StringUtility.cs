using System;
using System.Collections.Generic;
using System.Linq;

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
}
