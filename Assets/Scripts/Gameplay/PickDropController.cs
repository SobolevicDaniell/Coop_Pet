// Assets/Scripts/Gameplay/PickDropController.cs
using Fusion;
using UnityEngine;

namespace Game
{
    public class PickDropController
    {
        private readonly InteractionController _ic;
        private readonly ServerRpcHandler _rpc;

        public PickDropController(
            InteractionController ic,
            ServerRpcHandler rpc)
        {
            _ic = ic;
            _rpc = rpc;
        }

        public void TryPick()
        {
            var ray = _ic.Camera.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, _ic.Range)
             && hit.collider.TryGetComponent<PickableItem>(out var pickable))
            {
                _rpc.RPC_RequestPick(pickable.Object);
            }
        }

        public void UpdatePrompt()
        {
            var ray = _ic.Camera.ScreenPointToRay(Input.mousePosition);
            bool canPick = Physics.Raycast(ray, out var hit, _ic.Range)
                           && hit.collider.TryGetComponent<PickableItem>(out _);
            if (canPick) _ic.Prompt.Show();
            else _ic.Prompt.Hide();
        }
    }
}
