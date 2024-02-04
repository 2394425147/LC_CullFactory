using BepInEx;
using CullFactory.Extenders;
using HarmonyLib;
using UnityEngine;

namespace CullFactory;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BaseUnityPlugin
{
    private const string Guid = "com.fumiko.CullFactory";
    private const string Name = "CullFactory";
    private const string Version = "0.6.2";
    public static Plugin Instance { get; private set; }

    public static Config Configuration { get; private set; }

    private void Awake()
    {
        Instance = this;
        Configuration = new Config(Config);

        var harmony = new Harmony(Guid);
        harmony.PatchAll(typeof(LevelGenerationExtender));
        harmony.PatchAll(typeof(EntranceTeleportExtender));

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
}