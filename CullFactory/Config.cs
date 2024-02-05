using BepInEx.Configuration;

namespace CullFactory;

/// <summary>
///     Typed wrapper around the <see cref="BepInEx.Configuration.ConfigFile" /> class
/// </summary>
public sealed class Config
{
    public Config(ConfigFile configFile)
    {
        Logging = configFile.Bind("General",
                                  "Show culling logs",
                                  false,
                                  "View culled objects in the console.");
    }

    public ConfigEntry<bool> Logging { get; private set; }
}
