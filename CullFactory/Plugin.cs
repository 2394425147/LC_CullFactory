using BepInEx;
using CullFactory.Behaviours.Visualization;
using CullFactory.Extenders;
using HarmonyLib;
using UnityEngine;

namespace CullFactory;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.fumiko.CullFactory";
    public const string Name = "CullFactory";
    public const string Version = "1.0.0";
    public static Plugin Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
        CullFactory.Config.Initialize(Config);

        var harmony = new Harmony(Guid);
        harmony.PatchAll(typeof(LevelGenerationExtender));
        harmony.PatchAll(typeof(TeleportExtender));
        harmony.PatchAll(typeof(MapSeedOverride));
        harmony.PatchAll(typeof(GrabbableObjectExtender));

        QualitySettings.shadowResolution = ShadowResolution.Low;

        Log($"Plugin {Name} is loaded!");
    }

    public static void LogAlways(string s)
    {
        Instance.Logger.LogInfo(s);
    }

    public static void Log(string s)
    {
        if (!CullFactory.Config.Logging.Value)
            return;

        LogAlways(s);
    }

    public static void LogWarning(string s)
    {
        Instance.Logger.LogWarning(s);
    }

    public static void LogError(string s)
    {
        Instance.Logger.LogError(s);
    }

    public static void CreateCullingVisualizers()
    {
        if (RoundManager.Instance.dungeonGenerator == null)
            return;

        var dungeonObject = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject;

        DestroyImmediate(dungeonObject.GetComponent<CullingVisualizer>());
        var newVisualizer = dungeonObject.AddComponent<CullingVisualizer>();
        newVisualizer.RefreshVisualizers();
    }
}
