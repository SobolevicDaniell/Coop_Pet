using System;

namespace Game
{
    [Serializable]
    public class ItemState
    {
        public int ammo;
        public int durability;

        public ItemState() { }                      // нужен Unity и старым местам
        public ItemState(ItemState other)           // копирующий
        {
            if (other == null) return;
            ammo = other.ammo;
            durability = other.durability;
        }

        // ↓ добавь эти удобные перегрузки
        public ItemState(int ammo)                  // поддержка new ItemState(ammo)
        {
            this.ammo = ammo;
        }

        public ItemState(int ammo, int durability)  // на будущее, если где-то используют оба
        {
            this.ammo = ammo;
            this.durability = durability;
        }

        public ItemState Clone() => new ItemState(this);
    }


}