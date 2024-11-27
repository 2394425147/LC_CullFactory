using System;
using UnityEngine;

namespace CullFactory.Behaviours.API;

public class CameraCullingOptions : MonoBehaviour
{
    // Disables culling for this camera, causing all objects in the world to render regardless of camera position.
    // Avoid using this during normal gameplay scenarios.
    public bool DisableCulling { get; set; } = false;
    [Obsolete("Use DisableCulling")]
    public bool disableCulling { get => DisableCulling; set => value = DisableCulling; }
    // Skips culling so that this camera does not affect the visibility of any objects in its render pass.
    // This can be used to inspect culling results in a free cam, for example.
    public bool SkipCulling { get; set; } = false;
    [Obsolete("Use SkipCulling")]
    public bool skipCulling { get => SkipCulling; set => value = SkipCulling; }
}
