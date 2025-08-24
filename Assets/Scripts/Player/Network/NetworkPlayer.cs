using Fusion;
using Game.Network;
using Game;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] private PlayerMovement _movement;
    [SerializeField] private PlayerCameraController _cameraController;

    [Networked] private Vector3 ServerPosition { get; set; }

    public override void Spawned()
    {
        _cameraController.SetLocal(Object.HasInputAuthority);
    }

    public override void FixedUpdateNetwork()
    {
        if (GetInput(out InputData input))
            _movement.HandleInput(input, Runner.DeltaTime);

        if (Object.HasStateAuthority)
            ServerPosition = transform.position;
        else
            transform.position = Vector3.Lerp(transform.position, ServerPosition, 12f * Runner.DeltaTime);
    }

}