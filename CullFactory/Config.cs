using BepInEx.Configuration;

namespace CullFactory;

/// <summary>
/// Typed wrapper around the <see cref="BepInEx.Configuration.ConfigFile"/> class
/// </summary>
public sealed class Config
{
    public ConfigEntry<bool>  Logging                { get; private set; }
    public ConfigEntry<float> UpdateFrequency        { get; private set; }
    public ConfigEntry<bool>  UseAdjacentRoomTesting { get; private set; }
    public ConfigEntry<int>   MaxBranchingDepth      { get; private set; }
    public ConfigEntry<float> CullDistance           { get; private set; }
    public ConfigEntry<bool>  UseMultithreading      { get; private set; }

    public Config(ConfigFile configFile)
    {
        Logging = configFile.Bind("General",
                                  "Show culling logs",
                                  false,
                                  "View culled objects in the console.");

        UpdateFrequency = configFile.Bind("General",
                                          "Update frequency",
                                          5f,
                                          "Higher values make culling more responsive at the cost of performance.\n" +
                                          "Update interval: 1 / value (seconds)");

        UseAdjacentRoomTesting = configFile.Bind("Depth culling",
                                                 "Use depth culling",
                                                 true,
                                                 "Rooms that aren't adjacent to your own position will be culled.\n" +
                                                 "The recommended testing method. Disable only if you are experiencing performance issues.");

        MaxBranchingDepth = configFile.Bind("Depth culling",
                                            "Max branching depth",
                                            3,
                                            "How many doors can be traversed before a room is culled.");

        CullDistance = configFile.Bind("Distance culling",
                                       "Cull distance",
                                       40f,
                                       "Rooms that are this far from the player will be culled.\n" +
                                       "Used for monitoring other players and when adjacent room testing isn't available.");

        UseMultithreading = configFile.Bind("Distance culling",
                                            "Use multithreading",
                                            false,
                                            "Allocate a thread per room when checking distance to all enabled cameras.\n" +
                                            "May improve performance on computers with multiple cores.");
    }
}
