using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace CullFactory.Services;

public class IdentityEqualityComparer<T> : IEqualityComparer<T>
{
    public static readonly IdentityEqualityComparer<T> Instance = new();

    public bool Equals(T x, T y)
    {
        return ReferenceEquals(x, y);
    }

    public int GetHashCode(T obj)
    {
        return RuntimeHelpers.GetHashCode(obj);
    }
}
