using CullFactory.Extenders;
using UnityEngine;

namespace CullFactory.Behaviours;

public sealed class DynamicCuller : MonoBehaviour
{
    private const float CullDistance = 2048;

    private ManualCameraRenderer _monitor;

    private void OnEnable()
    {
        foreach (var cameraRenderer in FindObjectsByType<ManualCameraRenderer>(FindObjectsSortMode.None))
        {
            var isMonitorCamera = cameraRenderer.mapCamera != null;

            if (!isMonitorCamera)
                continue;

            _monitor = cameraRenderer;
            break;
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

            if (localPlayer != _monitor.targetedPlayer)
                shouldBeVisible |= Vector3.SqrMagnitude(position - _monitor.targetedPlayer.transform.position) <= CullDistance;

            meshContainer.SetVisible(shouldBeVisible);
        }
    }
}
