using UnityEngine;

namespace Moba.Runtime.Bootstrap
{
    /// <summary>
    /// Legacy runtime bootstrap helper.
    /// Scene objects are now saved directly into Game.unity in the Editor for full visual inspection and tweaking.
    /// </summary>
    public class MobaDemoBootstrap : MonoBehaviour
    {
        // Scene setup is now directly persisted in Game.unity.
        // This component remains available for manual runtime fallback if needed.
    }
}
