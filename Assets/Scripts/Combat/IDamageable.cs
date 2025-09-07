namespace Game
{
    public interface IDamageable
    {
        // Возврат true — урон принят
        bool ApplyDamage(in DamageInfo info);
    }
}
