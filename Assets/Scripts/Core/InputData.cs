using Fusion;
using UnityEngine;

public struct InputData : INetworkInput
{
    public Vector2 movement;
    public float mouseX;
    public float mouseY;
    public bool jump;
}
