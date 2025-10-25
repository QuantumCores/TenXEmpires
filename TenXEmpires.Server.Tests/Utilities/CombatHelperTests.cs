using FluentAssertions;
using TenXEmpires.Server.Domain.Entities;
using TenXEmpires.Server.Infrastructure.Services;

namespace TenXEmpires.Server.Tests.Utilities;

public class CombatHelperTests
{
    [Fact]
    public void ResolveAttack_WithMeleeUnits_AppliesDamageAndPossibleCounter()
    {
        var attackerType = new UnitDefinition { Attack = 20, Defence = 10, Health = 100, IsRanged = false };
        var defenderType = new UnitDefinition { Attack = 15, Defence = 12, Health = 100, IsRanged = false };

        var attacker = new Unit { Type = attackerType, Hp = 100 };
        var target = new Unit { Type = defenderType, Hp = 80 };

        var outcome = CombatHelper.ResolveAttack(attacker, target);

        outcome.AttackerDamage.Should().BeGreaterThan(0);
        target.Hp.Should().BeLessThan(80);
        // Defender may counter; if so, attacker HP reduces
        attacker.Hp.Should().BeLessThanOrEqualTo(100);
    }

    [Fact]
    public void ResolveAttackOnCity_ReducesCityHp_NoCounter()
    {
        var attackerType = new UnitDefinition { Attack = 25, Defence = 10, Health = 100, IsRanged = false };
        var attacker = new Unit { Type = attackerType, Hp = 100 };
        var city = new City { Hp = 50, MaxHp = 100 };

        var dmg = CombatHelper.ResolveAttackOnCity(attacker, city);

        dmg.Should().BeGreaterThan(0);
        city.Hp.Should().Be(50 - dmg);
        attacker.Hp.Should().Be(100); // no counter
    }
}

