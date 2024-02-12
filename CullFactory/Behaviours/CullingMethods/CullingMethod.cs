using System;
using CullFactory.Data;
using UnityEngine;

namespace CullFactory.Behaviours.CullingMethods;

public abstract class CullingMethod : MonoBehaviour
{
    private static CullingMethod Instance { get; set; }

    public static void Initialize()
    {
        if (Instance != null)
        {
            Destroy(Instance);
            Instance = null;
        }

        if (RoundManager.Instance.dungeonGenerator == null)
            return;

        var dungeon = RoundManager.Instance.dungeonGenerator.Generator.CurrentDungeon.gameObject;

        switch (Plugin.Configuration.Culler.Value)
        {
            case CullingType.PortalOcclusionCulling:
                Instance = dungeon.AddComponent<PortalOcclusionCuller>();
                break;
            case CullingType.DepthCulling:
                Instance = dungeon.AddComponent<DepthCuller>();
                break;
            case CullingType.None:
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}