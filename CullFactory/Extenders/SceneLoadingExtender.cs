using CullFactory.Behaviours.CullingMethods;
using UnityEngine.SceneManagement;

namespace CullFactory.Extenders;

internal static class SceneLoadingExtender
{
    internal static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode != LoadSceneMode.Additive)
            return;
        CullingMethod.Initialize();
    }
}
