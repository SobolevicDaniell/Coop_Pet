namespace Game
{
    public class DefaultHandBehavior : UnityEngine.MonoBehaviour, IHandItemBehavior
    {
        private ItemSO _so;
        public DefaultHandBehavior Construct(ItemSO so) { _so = so; return this; }

        public void OnEquip() { /* ничего */ }
        public void OnUnequip() { Destroy(gameObject); }
        public void OnUsePressed() { /* ничего */ }
        public void OnUseHeld(float d) { }
        public void OnUseReleased() { }
    }
}
