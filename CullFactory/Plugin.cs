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
        private const string Version = "0.4.2";

        private void Awake()
        {
            Instance      = this;
            Configuration = new Config(Config);

            var harmony = new Harmony(Guid);
            harmony.PatchAll(typeof(LevelGenerationExtender));

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
}
