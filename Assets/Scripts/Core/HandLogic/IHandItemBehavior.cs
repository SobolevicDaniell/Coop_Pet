namespace Game
{
    public interface IHandItemBehavior
    {
        void OnEquip();
        void OnUnequip();
        void OnUsePressed();          // нажатие кнопки (Down)
        void OnUseReleased();         // отпускание (Up)
        void OnUseHeld(float delta);  // удержание (каждый кадр)
        void OnMuzzleFlash();
    }
}
