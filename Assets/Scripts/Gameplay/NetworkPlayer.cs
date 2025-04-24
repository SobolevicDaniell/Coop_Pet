// Assets/Scripts/Network/NetworkPlayer.cs
using Fusion;
using Game;
using Game.Network;
using UnityEngine;

public class NetworkPlayer : NetworkBehaviour
{
    [SerializeField] private PlayerMovement _movement;
    [SerializeField] private PlayerCameraController _cameraController;

    [Networked] private Vector3 ServerPosition { get; set; }

    public override void Spawned()
    {
        if (Object.InputAuthority == Runner.LocalPlayer)
            ServerPosition = transform.position;
        bool isLocal = Object.InputAuthority == Runner.LocalPlayer;
        _cameraController.SetLocal(isLocal);
    }

    public override void FixedUpdateNetwork()
    {
        bool isLocal = Object.InputAuthority == Runner.LocalPlayer;
        bool isServer = Object.HasStateAuthority;

        if (isLocal && GetInput(out InputData input))
        {
            // клиентская предикция
            _movement.HandleInput(input, Runner.DeltaTime);
        }

        if (isServer && GetInput(out InputData serverInput))
        {
            // серверный авторитет
            _movement.HandleInput(serverInput, Runner.DeltaTime);
        }

        if (isServer)
        {
            ServerPosition = transform.position;
        }
        else if (isLocal)
        {
            // реконсиляция
            transform.position = Vector3.Lerp(transform.position, ServerPosition, 10f * Time.deltaTime);
        }
    }
}
