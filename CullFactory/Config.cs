using System.IO;
using System.Linq;
using System.Text;
using BepInEx.Configuration;
using CullFactory.Behaviours.CullingMethods;
using CullFactory.Data;
using CullFactory.Services;

namespace CullFactory;

/// <summary>
///     Typed wrapper around the <see cref="BepInEx.Configuration.ConfigFile" /> class
/// </summary>
public static class Config
{
    private static readonly string VersionFile = Path.Combine(BepInEx.Paths.PluginPath, "fumiko-CullFactory", "version");
    private static readonly string[] BaseSetOfInteriorsToUseFallbackPortals = ["BunkerFlow"];

    public static void Initialize(ConfigFile configFile)
    {
        #region General

        Culler = configFile.Bind("General",
                                 "Culling type",
                                 CullingType.PortalOcclusionCulling,
                                 "The culling type to use.\n\n" +
                                 "Options:\n" +
                                 "\"PortalOcclusionCulling\": Hides all the rooms that aren't visible to the camera (Recommended)\n" +
                                 "\"DepthCulling\": Hides rooms that aren't adjacent to the player's current room");

        UpdateFrequency = configFile.Bind("General",
                                          "Update frequency",
                                          5f,
                                          "Higher values make culling more responsive at the cost of performance.\n" +
                                          "Currently this has no effect when portal occlusion culling is used.\n" +
                                          "Update interval: 1 / value (seconds)");

        #endregion

        #region Portal Occlusion Culling

        InteriorsToUseFallbackPortals = configFile.Bind("Portal occlusion culling",
                                                        "Use fallback portals for interiors",
                                                        "",
                                                        "Use a more forgiving testing method for the specified interiors.\n" +
                                                        "This is recommended for interiors with incorrect portal sizes.\n\n" +
                                                        "Value:\n" +
                                                        "A list of dungeon generators, separated by commas \",\".");

        InteriorsToSkipFallbackPortals = configFile.Bind("Portal occlusion culling",
                                                         "Skip fallback portals for interiors",
                                                         "",
                                                         "Skip using the more forgiving testing method for the specified interiors.\n" +
                                                         "Can be enabled for interiors that are confirmed to have good portal sizes.\n\n" +
                                                         "Value:\n" +
                                                         "A list of dungeon generators, separated by commas \",\".");

        #endregion

        #region Depth Culling

        MaxBranchingDepth = configFile.Bind("Depth culling",
                                            "Max branching depth",
                                            4,
                                            "How many doors can be traversed before a room is culled.");

        #endregion

        #region Distance Culling

        CullDistanceEnabled = configFile.Bind("Distance culling",
                                              "Enabled",
                                              false,
                                              "Whether to override the camera's far plane distance. When " +
                                              "this is false, the 'Cull distance' and 'Surface cull distance' " +
                                              "options will have no effect.\n" +
                                              "If performance with portal occlusion culling enabled is insufficient " +
                                              "this may provide a small boost in performance.");

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

        #endregion

        #region Debug

        VisualizePortals = configFile.Bind("Debug",
                                           "Visualize portals",
                                           false,
                                           "Shows a rectangle representing the bounds of all portals that are used to " +
                                           "determine visibility when portal occlusion culling is enabled. If the portal " +
                                           "doesn't block the entirety of the visible portion of the next tile, then culling " +
                                           "will not be correct.");

        VisualizedPortalOutsetDistance = configFile.Bind("Debug",
                                                         "Visualized portal outset distance",
                                                         0.2f,
                                                         "The distance to offset each side of a portal visualizer out from the " +
                                                         "actual position of the portal. For doors that don't cover an entire tile " +
                                                         "wall, this allows seeing the exact bounds it covers.");

        VisualizeTileBounds = configFile.Bind("Debug",
                                              "Visualize tile bounds",
                                              false,
                                              "Shows a rectangular prism to represent the bounds all tiles. These bounds are used " +
                                              "to determine which tile a camera resides in.");

        OverrideMapSeed = configFile.Bind("Debug",
                                          "Override map seed",
                                          "",
                                          "INTENDED FOR BENCHMARKING ONLY. Leave this empty if you are playing normally.\n" +
                                          "This forces the map seed to be whatever is entered here, so that benchmarking " +
                                          "numbers can remain as consistent as possible between runs.");

        Logging = configFile.Bind("Debug",
                                  "Show culling logs",
                                  false,
                                  "View culling activity in the console.");

        #endregion

        MigrateSettings();

        Culler.SettingChanged += (_, _) => CullingMethod.Initialize();
        InteriorsToUseFallbackPortals.SettingChanged += (_, _) => UpdateInteriorsWithFallbackPortals();
        InteriorsToSkipFallbackPortals.SettingChanged += (_, _) => UpdateInteriorsWithFallbackPortals();
        VisualizePortals.SettingChanged += (_, _) => Plugin.CreateCullingVisualizers();
        VisualizedPortalOutsetDistance.SettingChanged += (_, _) => Plugin.CreateCullingVisualizers();
        VisualizeTileBounds.SettingChanged += (_, _) => Plugin.CreateCullingVisualizers();

        UpdateInteriorsWithFallbackPortals();
    }

    private static void MigrateSettings()
    {
        var versionBeforeLaunch =
            File.Exists(VersionFile) ? Encoding.UTF8.GetString(File.ReadAllBytes(VersionFile)) : Plugin.Version;

        if (versionBeforeLaunch != Plugin.Version && InteriorsToUseFallbackPortals.Value == "CastleFlow, SewerFlow")
            InteriorsToUseFallbackPortals.Value = "";

        using var writer = File.Open(VersionFile, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);
        writer.Write(Encoding.UTF8.GetBytes(Plugin.Version));
    }

    private static void UpdateInteriorsWithFallbackPortals()
    {
        InteriorsWithFallbackPortals = BaseSetOfInteriorsToUseFallbackPortals
                                       .Union(InteriorsToUseFallbackPortals.Value.SplitByComma())
                                       .Except(InteriorsToSkipFallbackPortals.Value.SplitByComma())
                                       .ToArray();
        DungeonCullingInfo.UpdateInteriorsWithFallbackPortals();
    }

    public static ConfigEntry<bool> Logging { get; private set; }
    public static ConfigEntry<CullingType> Culler { get; private set; }
    public static ConfigEntry<float> UpdateFrequency { get; private set; }

    /// <summary>
    /// <para>
    /// A comma-separated list of interior names where the portal occlusion culling should consider
    /// the portals to be the maximum size allowable within their tile.
    /// </para>
    /// <para>
    /// Can prevent compatibility issues with mods that don't correctly set the size of their doorway sockets.
    /// </para>
    /// </summary>
    public static ConfigEntry<string> InteriorsToUseFallbackPortals { get; private set; }

    /// <summary>
    /// <para>
    /// A comma-separated list of interior names to be removed from the list of interiors to use maximum-sized
    /// portals inside.
    /// </para>
    /// <para>
    /// Allows interiors with properly-sized doorway sockets to utilize those sizes instead of the fallback.
    /// </para>
    /// </summary>
    public static ConfigEntry<string> InteriorsToSkipFallbackPortals { get; private set; }

    public static ConfigEntry<int> MaxBranchingDepth { get; private set; }
    public static ConfigEntry<bool> CullDistanceEnabled { get; private set; }
    public static ConfigEntry<float> CullDistance { get; private set; }
    public static ConfigEntry<float> SurfaceCullDistance { get; private set; }
    public static ConfigEntry<string> OverrideMapSeed { get; private set; }
    public static ConfigEntry<bool> VisualizePortals { get; private set; }
    public static ConfigEntry<float> VisualizedPortalOutsetDistance { get; private set; }
    public static ConfigEntry<bool> VisualizeTileBounds { get; private set; }

    public static string[] InteriorsWithFallbackPortals { get; private set; }
}
