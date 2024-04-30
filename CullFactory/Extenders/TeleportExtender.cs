using CullFactory.Data;
using GameNetcodeStuff;
using HarmonyLib;

namespace CullFactory.Extenders;

[HarmonyPatch(typeof(EntranceTeleport))]
public static class TeleportExtender
{
    private static float[] _initialPlayerCameraFarPlanes;

    public static void SetInitialFarClipPlane()
    {
        var allPlayers = StartOfRound.Instance.allPlayerScripts;

        if (_initialPlayerCameraFarPlanes == null)
        {
            _initialPlayerCameraFarPlanes = new float[allPlayers.Length];
            for (var i = 0; i < allPlayers.Length; i++)
                _initialPlayerCameraFarPlanes[i] = allPlayers[i].gameplayCamera.farClipPlane;
        }

        if (!Config.CullDistanceEnabled.Value)
        {
            for (var i = 0; i < allPlayers.Length; i++)
                allPlayers[i].gameplayCamera.farClipPlane = _initialPlayerCameraFarPlanes[i];
            return;
        }

        for (var i = 0; i < allPlayers.Length; i++)
        {
            var player = allPlayers[i];
            player.gameplayCamera.farClipPlane = player.isInsideFactory
                                                     ? Config.CullDistance.Value
                                                     : Config.SurfaceCullDistance.Value;
            Plugin.Log($"Set culling distance of \"{player.gameplayCamera.name}\" to {player.gameplayCamera.farClipPlane}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.ConnectClientToPlayerObject))]
    private static void LocalPlayerTookControl()
    {
        SetInitialFarClipPlane();
        DynamicObjects.CollectAllPlayerLights();
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(PlayerControllerB), nameof(PlayerControllerB.TeleportPlayer))]
    private static void OnTeleportPlayerController(PlayerControllerB __instance)
    {
        OnPlayerTeleported(__instance);
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
        DynamicObjects.OnPlayerTeleported(player);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.SetEnemyOutside))]
    private static void OnEnemySetOutsideOrInside(EnemyAI __instance)
    {
        OnEnemyTeleported(__instance);
    }

    private static void OnEnemyTeleported(EnemyAI enemy)
    {
        DynamicObjects.OnEnemyTeleported(enemy);
    }

    private static void UpdateFarPlane(PlayerControllerB player)
    {
        if (!Config.CullDistanceEnabled.Value)
            return;

        player.gameplayCamera.farClipPlane = player.isInsideFactory
                                                 ? Config.CullDistance.Value
                                                 : Config.SurfaceCullDistance.Value;

        Plugin.Log($"{player.playerUsername} is{(player.isInsideFactory ? string.Empty : " not")} in the factory, set far plane distance to {player.gameplayCamera.farClipPlane}");
    }
}
