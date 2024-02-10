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
                                 "Portal occlusion culling tests what rooms are visible to the camera based on the size of the passages between them. " +
                                 "This is the recommended setting, as it should yield a significant performance gain on large maps without any visual change.\n" +
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

        OverrideMapSeed = configFile.Bind("Debug",
                                          "Override map seed",
                                          "",
                                          "INTENDED FOR BENCHMARKING ONLY. Leave this empty if you are playing normally.\n" +
                                          "This forces the map seed to be whatever is entered here, so that benchmarking " +
                                          "numbers can remain as consistent as possible between runs.");

        VisualizePortals = configFile.Bind("Debug",
                                           "Visualize portals",
                                           false,
                                           "Shows a rectangle representing the bounds of all portals that are used to " +
                                           "determine visibility when portal occlusion culling is enabled. If the portal " +
                                           "doesn't block the entirety of the visible portion of the next tile, then culling " +
                                           "will not be correct.");

        VisualizePortals.SettingChanged += (_, _) => Plugin.CreateCullingVisualizers();

        VisualizedPortalOutsetDistance = configFile.Bind("Debug",
                                                         "Visualized portal outset distance",
                                                         0.2f,
                                                         "The distance to offset each side of a portal visualizer out from the " +
                                                         "actual position of the portal. For doors that don't cover an entire tile " +
                                                         "wall, this allows seeing the exact bounds it covers.");

        VisualizedPortalOutsetDistance.SettingChanged += (_, _) => Plugin.CreateCullingVisualizers();

        VisualizeTileBounds = configFile.Bind("Debug",
                                              "Visualize tile bounds",
                                              false,
                                              "Shows a rectangular prism to represent the bounds all tiles. These bounds are used " +
                                              "to determine which tile a camera resides in.");

        VisualizeTileBounds.SettingChanged += (_, _) => Plugin.CreateCullingVisualizers();
    }

    public ConfigEntry<bool> Logging { get; private set; }
    public ConfigEntry<CullingType> Culler { get; private set; }
    public ConfigEntry<float> UpdateFrequency { get; private set; }
    public ConfigEntry<int> MaxBranchingDepth { get; private set; }
    public ConfigEntry<float> CullDistance { get; private set; }
    public ConfigEntry<float> SurfaceCullDistance { get; private set; }
    public ConfigEntry<string> OverrideMapSeed { get; private set; }
    public ConfigEntry<bool> VisualizePortals { get; private set; }
    public ConfigEntry<float> VisualizedPortalOutsetDistance { get; private set; }
    public ConfigEntry<bool> VisualizeTileBounds { get; private set; }
}
