using UnityEngine;
using Fusion.Addons.Physics;

namespace Game.Network
{
    public sealed class RunnerPhysicsBootstrap : MonoBehaviour
    {
        private void Awake()
        {
            if (GetComponent<RunnerSimulatePhysics3D>() == null)
                gameObject.AddComponent<RunnerSimulatePhysics3D>();
        }
    }
}
