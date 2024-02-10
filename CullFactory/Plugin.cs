using BepInEx;
using CullFactory.Behaviours;
using CullFactory.Extenders;
using HarmonyLib;
using UnityEngine;

namespace CullFactory;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    private const string Guid = "com.fumiko.CullFactory";
    private const string Name = "CullFactory";
    private const string Version = "0.7.1";
    public static Plugin Instance { get; private set; }

    public static Config Configuration { get; private set; }

    private void Awake()
    {
        Instance = this;
        Configuration = new Config(Config);

        var harmony = new Harmony(Guid);
        harmony.PatchAll(typeof(LevelGenerationExtender));
        harmony.PatchAll(typeof(EntranceTeleportExtender));
        harmony.PatchAll(typeof(MapSeedOverride));

        QualitySettings.shadowResolution = ShadowResolution.Low;

        Log($"Plugin {Name} is loaded!");
    }

    public static void Log(string s)
    {
        if (!Configuration.Logging.Value)
            return;

        Instance.Logger.LogInfo(s);
    }

    public static void LogError(string s)
    {
        Instance.Logger.LogError(s);
    }

    public static void CreateCullingHandler()
    {
        if (RoundManager.Instance.dungeonGenerator == null)
            return;

        var dungeonObject = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject;
        Destroy(dungeonObject.GetComponent<DynamicCuller>());
        Destroy(dungeonObject.GetComponent<PortalOcclusionCuller>());

        switch (Configuration.Culler.Value)
        {
            case CullingType.PortalOcclusionCulling:
                dungeonObject.AddComponent<PortalOcclusionCuller>();
                break;
            case CullingType.DepthCulling:
                dungeonObject.AddComponent<DynamicCuller>();
                break;
        }
    }

    public static void CreateCullingVisualizers()
    {
        if (RoundManager.Instance.dungeonGenerator == null)
            return;

        var dungeonObject = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject;

        var visualizer = dungeonObject.GetComponent<CullingVisualizer>() ?? dungeonObject.AddComponent<CullingVisualizer>();
        visualizer.RefreshVisualizers();
    }
}
