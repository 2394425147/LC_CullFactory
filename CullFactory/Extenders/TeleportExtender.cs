using GameNetcodeStuff;
using HarmonyLib;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(EntranceTeleport))]
public static class TeleportExtender
{
    private static float[] _playerGameplayCameraFarPlanes;

    public static void SetInitialFarClipPlane()
    {
        var allPlayers = StartOfRound.Instance.allPlayerScripts;

        if (!Config.CullDistanceEnabled.Value)
        {
            if (_playerGameplayCameraFarPlanes == null)
                return;
            for (var i = 0; i < allPlayers.Length; i++)
                allPlayers[i].gameplayCamera.farClipPlane = _playerGameplayCameraFarPlanes[i];
            return;
        }

        _playerGameplayCameraFarPlanes = new float[allPlayers.Length];

        for (var i = 0; i < allPlayers.Length; i++)
        {
            var player = allPlayers[i];
            _playerGameplayCameraFarPlanes[i] = player.gameplayCamera.farClipPlane;
            player.gameplayCamera.farClipPlane = player.isInsideFactory
                                                     ? Config.CullDistance.Value
                                                     : Config.SurfaceCullDistance.Value;
            Plugin.Log($"Set culling distance of \"{player.gameplayCamera.name}\" to {player.gameplayCamera.farClipPlane}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.TeleportPlayer))]
    private static void OnTeleportPlayerController(PlayerControllerB __instance)
    {
        OnPlayerTeleported(__instance);
    }

    // EntranceTeleport sets the `isInsideFactory` flag after calling `PlayerControllerB.TeleportPlayer()`,
    // so we need to postfix these too.

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayer))]
    private static void OnTeleportLocalPlayerThroughEntrance()
    {
        OnPlayerTeleported(StartOfRound.Instance.localPlayerController);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EntranceTeleport), nameof(EntranceTeleport.TeleportPlayerClientRpc))]
    private static void OnTeleportOtherPlayerThroughEntrance(ref int playerObj)
    {
        var player = StartOfRound.Instance.allPlayerScripts[playerObj];
        if (player == StartOfRound.Instance.localPlayerController)
            return;
        OnPlayerTeleported(player);
    }

    private static void OnPlayerTeleported(PlayerControllerB player)
    {
        UpdateFarPlane(player);
    }

    private static void UpdateFarPlane(PlayerControllerB player)
    {
        if (!Config.CullDistanceEnabled.Value)
            return;

        player.gameplayCamera.farClipPlane = player.isInsideFactory
                                                 ? Config.CullDistance.Value
                                                 : Config.SurfaceCullDistance.Value;

        Plugin.Log($"{player.playerUsername} is{(player.isInsideFactory ? "" : " not")} in the factory, set far plane distance to {player.gameplayCamera.farClipPlane}");
    }
}
