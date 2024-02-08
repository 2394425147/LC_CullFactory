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

        Culler = configFile.Bind("General",
                                 "Culling type",
                                 CullingType.PortalOcclusionCulling,
                                 "The culling type to use.\n" +
                                 "Portal occlusion culling tests what rooms are visible to the camera based on the size of the passages between them.\n" +
                                 "Depth culling hides rooms based on the number of rooms separating them from the camera.");

        Culler.SettingChanged += (_, _) => Plugin.CreateCullingHandler();

        UpdateFrequency = configFile.Bind("General",
                                          "Update frequency",
                                          5f,
                                          "Higher values make culling more responsive at the cost of performance.\n" +
                                          "Update interval: 1 / value (seconds)");

        MaxBranchingDepth = configFile.Bind("Depth culling",
                                            "Max branching depth",
                                            4,
                                            "How many doors can be traversed before a room is culled.");

        CullDistance = configFile.Bind("Distance culling",
                                       "Cull distance",
                                       40f,
                                       "The camera's far plane distance.\n" +
                                       "Objects that are this far from the player will be culled.\n" +
                                       "Vanilla value: 400");

        SurfaceCullDistance = configFile.Bind("Distance culling",
                                              "Surface cull distance",
                                              200f,
                                              "The camera's far plane distance when **on the surface**.\n" +
                                              "Objects that are this far from the player will be culled.\n" +
                                              "Vanilla value: 400");
    }

    public ConfigEntry<bool> Logging { get; private set; }
    public ConfigEntry<CullingType> Culler { get; private set; }
    public ConfigEntry<float> UpdateFrequency { get; private set; }
    public ConfigEntry<int> MaxBranchingDepth { get; private set; }
    public ConfigEntry<float> CullDistance { get; private set; }
    public ConfigEntry<float> SurfaceCullDistance { get; private set; }
}
