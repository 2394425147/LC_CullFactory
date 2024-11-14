using BepInEx;
using CullFactory.Behaviours.Visualization;
using CullFactory.Extenders;
using HarmonyLib;
using System.IO;
using UnityEngine;
using Unity.Burst;

namespace CullFactory;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    public const string Guid = "com.fumiko.CullFactory";
    public const string Name = "CullFactory";
    public const string Version = "1.5.0";
    public static Plugin Instance { get; private set; }

    private Harmony _harmony = new Harmony(Guid);

    private void Awake()
    {
        Instance = this;
        CullFactory.Config.Initialize(Config);

        _harmony.PatchAll(typeof(LevelGenerationExtender));
        _harmony.PatchAll(typeof(TeleportExtender));
        _harmony.PatchAll(typeof(MapSeedOverride));
        _harmony.PatchAll(typeof(GrabbableObjectExtender));

        QualitySettings.shadowResolution = ShadowResolution.Low;

        LoadBurstAssembly();

        Log($"Plugin {Name} is loaded!");
    }

    private void LoadBurstAssembly()
    {
        const string burstLibFilename = "lib_burst_generated.data";
        const string errorMessage = "culling may be slower than normal.";

        var workingDirectory = new FileInfo(Info.Location).DirectoryName;
        var burstLibrary = Path.Combine(workingDirectory, burstLibFilename);

        if (!File.Exists(burstLibrary))
        {
            LogError($"{Name}'s Burst assembly '{burstLibFilename}' was not found, {errorMessage}");
            return;
        }

        if (!BurstRuntime.LoadAdditionalLibrary(burstLibrary))
        {
            LogError($"{Name}'s Burst assembly failed to load, {errorMessage}");
            return;
        }

        if (!CullFactoryBurst.Plugin.IsRunningBurstLibrary())
        {
            LogError($"{Name}'s Burst plugin is not running its Burst-compiled code, {errorMessage}");
            return;
        }

        LogAlways($"Loaded {Name}'s Burst assembly.");

        // This patch must run after the Burst assembly is loaded, or BurstCompilerHelper.IsBurstGenerated
        // is evaluated before its IsBurstEnabled() method can be resolved to the native version, meaning
        // its value will remain false and no Burst methods will be resolved.
        _harmony.PatchAll(typeof(BurstErrorPrevention));
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
