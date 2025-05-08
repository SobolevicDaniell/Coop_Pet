namespace Game
{
    public class DefaultHandBehavior : UnityEngine.MonoBehaviour, IHandItemBehavior
    {
        private ItemSO _so;
        public DefaultHandBehavior Construct(ItemSO so) { _so = so; return this; }

        public void OnEquip() { /* ������ */ }
        public void OnUnequip() { Destroy(gameObject); }
        public void OnUsePressed() { /* ������ */ }
        public void OnUseHeld(float d) { }
        public void OnUseReleased() { }
    }
}
