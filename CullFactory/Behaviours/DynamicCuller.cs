using System.Collections.Generic;
using CullFactory.Extenders;
using UnityEngine;

namespace CullFactory.Behaviours;

/// <summary>
/// DynamicCuller instances are tied to each moon
/// </summary>
public sealed class DynamicCuller : MonoBehaviour
{
    private const float CullDistance = 45 * 45;

    private static readonly List<ManualCameraRenderer> Monitors = new();

    private void OnEnable()
    {
        if (Monitors.Count != 0)
            return;

        foreach (var cameraRenderer in FindObjectsByType<ManualCameraRenderer>(FindObjectsSortMode.None))
        {
            var isMonitorCamera = cameraRenderer.mapCamera != null;

            if (!isMonitorCamera)
                continue;

            Monitors.Add(cameraRenderer);
        }
    }

    public void Update()
    {
        var localPlayer = GameNetworkManager.Instance.localPlayerController.hasBegunSpectating
                              ? GameNetworkManager.Instance.localPlayerController.spectatedPlayerScript
                              : GameNetworkManager.Instance.localPlayerController;

        foreach (var meshContainer in LevelGenerationExtender.Tiles)
        {
            var position = meshContainer.parentTile.transform.position;

            var shouldBeVisible = Vector3.SqrMagnitude(position - localPlayer.transform.position) <= CullDistance;

            foreach (var monitor in Monitors)
            {
                if (!monitor.mapCamera.enabled)
                    continue;

                shouldBeVisible |= Vector3.SqrMagnitude(position - monitor.targetedPlayer.transform.position) <= CullDistance;
            }

            meshContainer.SetVisible(shouldBeVisible);
        }
    }
}
