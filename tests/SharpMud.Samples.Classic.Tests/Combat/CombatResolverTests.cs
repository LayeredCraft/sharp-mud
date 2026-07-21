using SharpMud.Engine.Core;
using SharpMud.Ruleset.Classic;

namespace SharpMud.Samples.Classic.Tests.Combat;

public sealed class CombatResolverTests
{
    [Fact]
    public void ResolveRound_AppliesDamageAndReportsHit_WhenToHitRollMeetsArmorClass()
    {
        var random = Substitute.For<IRandomSource>();
        random.Next(1, 20).Returns(10);
        random.Next(2, 5).Returns(3);

        var attacker = MakeCombatant("Attacker", damageMin: 2, damageMax: 5);
        var defender = MakeCombatant("Defender", armorClass: 10, hitPoints: 10);

        var sut = new CombatResolver(random);

        var result = sut.ResolveRound(attacker, defender);

        result.Hit.Should().BeTrue();
        result.Damage.Should().Be(3);
        defender.FindBehavior<CombatantBehavior>()!.CurrentHitPoints.Should().Be(7);
        result.DefenderDefeated.Should().BeFalse();
    }

    [Fact]
    public void ResolveRound_ReportsMissAndAppliesNoDamage_WhenToHitRollIsBelowArmorClass()
    {
        var random = Substitute.For<IRandomSource>();
        random.Next(1, 20).Returns(5);

        var attacker = MakeCombatant("Attacker");
        var defender = MakeCombatant("Defender", armorClass: 10, hitPoints: 10);

        var sut = new CombatResolver(random);

        var result = sut.ResolveRound(attacker, defender);

        result.Hit.Should().BeFalse();
        result.Damage.Should().Be(0);
        defender.FindBehavior<CombatantBehavior>()!.CurrentHitPoints.Should().Be(10);
    }

    [Fact]
    public void ResolveRound_ReportsDefenderDefeated_WhenDamageDropsHitPointsToZeroOrBelow()
    {
        var random = Substitute.For<IRandomSource>();
        random.Next(1, 20).Returns(20);
        random.Next(1, 4).Returns(4);

        var attacker = MakeCombatant("Attacker", damageMin: 1, damageMax: 4);
        var defender = MakeCombatant("Defender", armorClass: 10, hitPoints: 3);

        var sut = new CombatResolver(random);

        var result = sut.ResolveRound(attacker, defender);

        result.DefenderDefeated.Should().BeTrue();
        defender.FindBehavior<CombatantBehavior>()!.CurrentHitPoints.Should().BeLessThanOrEqualTo(0);
    }

    private static Thing MakeCombatant(
        string name, int armorClass = 0, int hitPoints = 10, int damageMin = 1, int damageMax = 4)
    {
        var thing = new Thing { Id = ThingId.New(), Name = name };
        thing.Behaviors.Add(new CombatantBehavior
        {
            MaxHitPoints = hitPoints,
            CurrentHitPoints = hitPoints,
            ArmorClass = armorClass,
            DamageMin = damageMin,
            DamageMax = damageMax,
        });
        return thing;
    }
}
