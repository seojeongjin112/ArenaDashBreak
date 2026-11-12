namespace ArenaBreak.Core
{
    // 공격자가 대상이 플레이어인지 적인지 몰라도 되도록 피격 계약만 분리한다
    public interface IDamageable
    {
        void TakeDamage(int amount);
    }
}
