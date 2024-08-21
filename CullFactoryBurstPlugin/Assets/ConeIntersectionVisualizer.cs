using CullFactoryBurst;
using UnityEngine;

public class ConeIntersectionVisualizer : MonoBehaviour
{
    [SerializeField]
    public Light Light;

    private void OnDrawGizmos()
    {
        var bounds = GetComponent<Renderer>().bounds;
        if (Geometry.SpotLightInfluencesBounds(Light, bounds))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawCube(bounds.center, bounds.extents * 2 + Vector3.one * 0.01f);
        }
    }
}
