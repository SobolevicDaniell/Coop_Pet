using System;

namespace Game
{
    [Serializable]
    public class ItemState
    {
        public int Ammo;

        public ItemState(int ammo = 0)
        {
            Ammo = ammo;
        }
    }
}