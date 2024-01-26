using BepInEx.Configuration;

namespace CullFactory;

/// <summary>
/// Typed wrapper around the <see cref="BepInEx.Configuration.ConfigFile"/> class
/// </summary>
public sealed class Config
{
    public ConfigEntry<bool>  UseAdjacentRoomTesting      { get; private set; }
    public ConfigEntry<int>   MaxBranchingDepth           { get; private set; }
    public ConfigEntry<float> AdjacentRoomUpdateFrequency { get; private set; }

    public ConfigEntry<float> CullDistance           { get; private set; }
    public ConfigEntry<bool>  UseMultithreading      { get; private set; }

    public Config(ConfigFile config)
    {
        UseAdjacentRoomTesting = config.Bind("Depth culling",
                                             "Use depth culling",
                                             true,
                                             "Rooms that aren't adjacent to your own position will be culled.\n" +
                                             "The recommended testing method. Disable only if you are experiencing performance issues.\n" +
                                             "Default value: true");

        MaxBranchingDepth = config.Bind("Depth culling",
                                        "Max branching depth",
                                        3,
                                        "How many doors can be traversed before a room is culled.\n" +
                                        "Default value: 3");

        AdjacentRoomUpdateFrequency = config.Bind("Depth culling",
                                                  "Update frequency",
                                                  5f,
                                                  "How often the depth culling is updated.\n" +
                                                  "Higher values make depth culling more accurate at the cost of performance.\n" +
                                                  "Default value: 5 (0.2 seconds)");

        CullDistance = config.Bind("Distance culling",
                                   "Cull distance",
                                   40f,
                                   "Rooms that are this far from the player will be culled.\n"                           +
                                   "Used for monitoring other players and when adjacent room testing isn't available.\n" +
                                   "Default value: 40");

        UseMultithreading = config.Bind("Distance culling",
                                   "Use multithreading",
                                   true,
                                   "Allocate a thread per room when checking distance to all enabled cameras.\n" +
                                   "May improve performance on computers with multiple cores.\n"                 +
                                   "Default value: true");
    }
}
