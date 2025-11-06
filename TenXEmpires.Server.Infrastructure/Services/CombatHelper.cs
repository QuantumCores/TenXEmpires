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
        // Compute primary damage: attacker deals damage to target
        // Parameters: attacker's attack stat, target's defense stat, attacker's HP (dealing unit), target's HP (receiving unit)
        var attackerDamage = ComputeDamage(
            attacker.Type.Attack,        // Attacking unit's attack stat
            target.Type.Defence,          // Defending unit's defense stat
            attacker.Hp,                  // Attacking unit's current HP (for damage scaling)
            attacker.Type.Health,         // Attacking unit's max HP
            target.Hp,                    // Defending unit's current HP (for defense scaling)
            target.Type.Health);          // Defending unit's max HP

        // Apply damage to target
        target.Hp -= attackerDamage;
        target.UpdatedAt = DateTimeOffset.UtcNow;

        // Counterattack: if target survives and both are melee, target counterattacks
        int? counterDamage = null;
        if (target.Hp > 0)
        {
            var defenderCanCounter = !target.Type.IsRanged;
            var attackerReceivesCounter = !attacker.Type.IsRanged;
            if (defenderCanCounter && attackerReceivesCounter)
            {
                // Compute counterattack damage: target (now attacker) deals damage to original attacker (now defender)
                // Note: target.Hp is AFTER taking the initial damage, so counterattack is weaker
                // Parameters: target's attack stat, attacker's defense stat, target's HP (dealing unit, reduced), attacker's HP (receiving unit, full)
                counterDamage = ComputeDamage(
                    target.Type.Attack,        // Counterattacking unit's attack stat
                    attacker.Type.Defence,      // Original attacker's defense stat
                    target.Hp,                  // Counterattacking unit's current HP (reduced from initial attack)
                    target.Type.Health,         // Counterattacking unit's max HP
                    attacker.Hp,                // Original attacker's current HP (full, before counterattack)
                    attacker.Type.Health);      // Original attacker's max HP

                // Apply counterattack damage to original attacker
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
