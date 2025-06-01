using System;

namespace Game
{
    public class ItemState
    {
        public int Ammo;

        public ItemState() { }

        // Копирующий конструктор
        public ItemState(ItemState state)
        {
            if (state != null)
                Ammo = state.Ammo;
        }

        public ItemState(int ammo)
        {
            Ammo = ammo;
        }
    }
}
