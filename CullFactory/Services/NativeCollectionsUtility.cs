using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace CullFactory.Services;

internal static class NativeCollectionsUtility
{
    public static unsafe T* GetPtr<T>(this NativeArray<T> array) where T : unmanaged
    {
        return (T*)array.GetUnsafePtr();
    }

    public static unsafe T* GetPtr<T>(this NativeSlice<T> array) where T : unmanaged
    {
        return (T*)array.GetUnsafePtr();
    }
}
