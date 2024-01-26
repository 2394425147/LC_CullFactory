using BepInEx;
using CullFactory.Extenders;
using HarmonyLib;

namespace CullFactory
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance { get; private set; }

        public static Config Configuration { get; private set; }

        private const string Guid    = "com.fumiko.CullFactory";
        private const string Name    = "CullFactory";
        private const string Version = "0.1.3";

        private void Awake()
        {
            Instance      = this;
            Configuration = new Config(Config);

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(LevelGenerationExtender));

            Logger.LogInfo($"Plugin {Name} is loaded!");
        }

        public static void LogError(string s)
        {
            Instance.Logger.LogError(s);

        }

        public static void Log(string s)
        {
            Instance.Logger.LogInfo(s);
        }
    }
}
