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
    public ConfigEntry<float> SurfaceCullDistance    { get; private set; }

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
                                            4,
                                            "How many doors can be traversed before a room is culled.");

        CullDistance = configFile.Bind("Distance culling",
                                       "Cull distance",
                                       40f,
                                       "The camera's far plane distance.\n"                          +
                                       "Objects that are this far from the player will be culled.\n" +
                                       "Vanilla value: 400");

        SurfaceCullDistance = configFile.Bind("Distance culling",
                                              "Surface cull distance",
                                              200f,
                                              "The camera's far plane distance when **on the surface**.\n"  +
                                              "Objects that are this far from the player will be culled.\n" +
                                              "Vanilla value: 400");
    }
}
