using Fusion;
using UnityEngine;

namespace Game
{
    public sealed class PickableItem : NetworkBehaviour
    {
        [Networked] public NetworkString<_32> ItemIdN { get; set; }
        [Networked] public int CountN { get; set; }
        [Networked] public int AmmoN { get; set; }
        [Networked] public bool Consumed { get; set; }

        [SerializeField] private string _initItemId;
        [SerializeField] private int _initCount;
        [SerializeField] private int _initAmmo;
        private bool _applied;
        private bool _consumedLocal;

        public void Initialize(string itemId, int count)
        {
            _initItemId = itemId;
            _initCount = count;
        }

        public void Initialize(string itemId, int count, int ammo)
        {
            _initItemId = itemId;
            _initCount = count;
            _initAmmo = ammo;
        }

        public void ServerInit(string itemId, int count, int ammo)
        {
            _initItemId = itemId;
            _initCount = count;
            _initAmmo = ammo;
            if (Runner != null && HasStateAuthority)
            {
                ItemIdN = itemId;
                CountN = count;
                AmmoN = ammo;
                Consumed = false;
                _applied = true;
            }
        }

        public override void Spawned()
        {
            if (HasStateAuthority && !_applied)
            {
                ItemIdN = _initItemId;
                CountN = _initCount;
                AmmoN = _initAmmo;
                Consumed = false;
                _applied = true;
            }
        }

        public string GetItemId()
        {
            return Runner == null ? _initItemId : ItemIdN.ToString();
        }

        public int GetCount()
        {
            return Runner == null ? _initCount : CountN;
        }

        public int GetAmmo()
        {
            return Runner == null ? _initAmmo : AmmoN;
        }

        public void SetCount(int value)
        {
            _initCount = value;
            if (Runner != null && HasStateAuthority)
                CountN = value;
        }

        public bool TryConsumeServer()
        {
            if (!HasStateAuthority) return false;
            if (Runner == null) { _consumedLocal = true; return true; }
            if (Consumed) return false;
            Consumed = true;
            return true;
        }
    }
}
