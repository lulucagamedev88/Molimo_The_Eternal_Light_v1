namespace MolimoByLulucaGameDev
{

    public interface IDamageable
    {
        float CurrentHealth { get; }
        float MaxHealth { get; }
        bool IsAlive { get; }
        void TakeDamage(float amount, float knockbackDistance = -1f);
    }
}
