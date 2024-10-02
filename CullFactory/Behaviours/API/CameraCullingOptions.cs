using UnityEngine;

namespace CullFactory.Behaviours.API;

public class CameraCullingOptions : MonoBehaviour
{
    // Disables culling for this camera, causing all objects in the world to render regardless of camera position.
    // Avoid using this during normal gameplay scenarios.
    public bool disableCulling { get; set; } = false;
    // Skips culling so that this camera does not affect the visibility of any objects in its render pass.
    // This can be used to inspect culling results in a free cam, for example.
    public bool skipCulling { get; set; } = false;
}
