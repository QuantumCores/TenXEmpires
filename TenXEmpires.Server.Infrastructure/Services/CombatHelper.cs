using TenXEmpires.Server.Domain.Entities;

namespace TenXEmpires.Server.Infrastructure.Services;

internal static class CombatHelper
{
    public readonly struct AttackOutcome
    {
        public AttackOutcome(int attackerDamage, int? counterDamage, bool targetDied, bool attackerDied)
        {
            AttackerDamage = attackerDamage;
            CounterDamage = counterDamage;
            TargetDied = targetDied;
            AttackerDied = attackerDied;
        }

        public int AttackerDamage { get; }
        public int? CounterDamage { get; }
        public bool TargetDied { get; }
        public bool AttackerDied { get; }
    }

    public static int ComputeDamage(
        int atkStat,
        int defStat,
        int attackerHp,
        int attackerMaxHp,
        int defenderHp,
        int defenderMaxHp)
    {
        var atkRatio = attackerMaxHp > 0 ? Math.Clamp(attackerHp / (double)attackerMaxHp, 0.0, 1.0) : 1.0;
        var defRatio = defenderMaxHp > 0 ? Math.Clamp(defenderHp / (double)defenderMaxHp, 0.0, 1.0) : 1.0;
        var atk = atkStat * atkRatio;
        var def = defStat * defRatio;
        if (def <= 0) def = 1; // safety
        var value = (atk * (1.0 + (atk - def) / def) * 0.5);
        var rounded = (int)Math.Round(value, 0, MidpointRounding.AwayFromZero);
        return Math.Max(1, rounded);
    }

    public static AttackOutcome ResolveAttack(Unit attacker, Unit target)
    {
        // Compute primary damage
        var attackerDamage = ComputeDamage(
            attacker.Type.Attack,
            target.Type.Defence,
            attacker.Hp,
            attacker.Type.Health,
            target.Hp,
            target.Type.Health);

        target.Hp -= attackerDamage;
        target.UpdatedAt = DateTimeOffset.UtcNow;

        int? counterDamage = null;
        if (target.Hp > 0)
        {
            var defenderCanCounter = !target.Type.IsRanged;
            var attackerReceivesCounter = !attacker.Type.IsRanged;
            if (defenderCanCounter && attackerReceivesCounter)
            {
                counterDamage = ComputeDamage(
                    target.Type.Attack,
                    attacker.Type.Defence,
                    target.Hp,
                    target.Type.Health,
                    attacker.Hp,
                    attacker.Type.Health);
                attacker.Hp -= counterDamage.Value;
                attacker.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }

        var targetDied = target.Hp <= 0;
        var attackerDied = attacker.Hp <= 0;

        return new AttackOutcome(attackerDamage, counterDamage, targetDied, attackerDied);
    }

    public static int ResolveAttackOnCity(Unit attacker, City city)
    {
        // City does not counterattack; use a simple defence baseline derived from its fortification.
        // We reuse ComputeDamage to keep damage scaling consistent with units.
        const int CityDefence = 15; // centralized baseline; adjust as balancing evolves
        var dmg = ComputeDamage(
            attacker.Type.Attack,
            CityDefence,
            attacker.Hp,
            attacker.Type.Health,
            city.Hp,
            city.MaxHp);

        city.Hp -= dmg;
        if (city.Hp < 0) city.Hp = 0;
        return dmg;
    }
}
