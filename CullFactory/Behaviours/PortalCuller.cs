using System.Collections.Generic;
using CullFactory.Data;
using CullFactory.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace CullFactory.Behaviours;

/// <summary>
///     DynamicCuller instances are tied to each moon
/// </summary>
public sealed class PortalCuller : MonoBehaviour
{
    private static PortalCuller Instance { get; set; }

    private void OnEnable()
    {
        if (Instance != null)
            Destroy(Instance);

        Instance = this;
        RenderPipelineManager.beginCameraRendering += OnCameraRender;
    }

    private static readonly HashSet<TileContents> TilesToCull = new();
    private static readonly Queue<FrustumAtDoor> FrustumQueue = new();

    private static void OnCameraRender(ScriptableRenderContext context, Camera camera)
    {
        if (!Keyboard.current.tabKey.isPressed)
            return;

        TilesToCull.Clear();
        FrustumQueue.Clear();

        CullForCamera(camera);

        foreach (var tile in DungeonUtilities.AllTiles)
            tile.SetActive(TilesToCull.Contains(tile));
    }

    private static void CullForCamera(Camera camera)
    {
        var origin = camera.transform.position.GetTile();

        if (origin == null)
            return;

        TilesToCull.Add(origin.GetContents());

        var fullFrustum = GeometryUtility.CalculateFrustumPlanes(camera);

        foreach (var doorway in origin.UsedDoorways)
        {
            if (doorway.TryTrimFrustum(camera, fullFrustum, out var frustumAtDoor))
                FrustumQueue.Enqueue(frustumAtDoor);
        }

        while (FrustumQueue.Count > 0)
        {
            var frustumAtCurrentDoor = FrustumQueue.Dequeue();
            TilesToCull.Add(frustumAtCurrentDoor.door.tile.GetContents());

            foreach (var doorway in frustumAtCurrentDoor.door.connectedDoorway.tile.UsedDoorways)
            {
                if (doorway == frustumAtCurrentDoor.door.connectedDoorway)
                    continue;

                if (doorway.TryTrimFrustum(camera, frustumAtCurrentDoor.frustum, out var frustumAtDoor))
                    FrustumQueue.Enqueue(frustumAtDoor);
            }
        }
    }

    private void OnDestroy()
    {
        Instance = null;
        RenderPipelineManager.beginCameraRendering -= OnCameraRender;
    }
}
