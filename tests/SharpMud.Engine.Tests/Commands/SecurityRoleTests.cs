using SharpMud.Engine.Commands;

namespace SharpMud.Engine.Tests.Commands;

public sealed class SecurityRoleTests
{
    private static readonly SecurityRole[] IndividualRoles =
    [
        SecurityRole.Mobile,
        SecurityRole.Item,
        SecurityRole.Room,
        SecurityRole.TutorialPlayer,
        SecurityRole.Player,
        SecurityRole.Helper,
        SecurityRole.Married,
        SecurityRole.MinorBuilder,
        SecurityRole.FullBuilder,
        SecurityRole.MinorAdmin,
        SecurityRole.FullAdmin,
    ];

    // The regression test for the undefined-values gap: if SecurityRole
    // were ever implemented with auto-numbered members instead of explicit
    // power-of-two values, at least one pair here would share a bit and
    // this test would catch it immediately.
    [Fact]
    public void EveryIndividualRole_IsADistinctPowerOfTwo()
    {
        foreach (var role in IndividualRoles)
        {
            var value = (uint)role;
            (value != 0 && (value & (value - 1)) == 0).Should().BeTrue($"{role} must be a single bit");
        }

        for (var i = 0; i < IndividualRoles.Length; i++)
        {
            for (var j = i + 1; j < IndividualRoles.Length; j++)
                (IndividualRoles[i] & IndividualRoles[j]).Should().Be(SecurityRole.None, $"{IndividualRoles[i]} and {IndividualRoles[j]} must not share a bit");
        }
    }

    [Fact]
    public void All_EqualsTheUnionOfEveryIndividualRole()
    {
        var union = IndividualRoles.Aggregate(SecurityRole.None, (acc, role) => acc | role);

        SecurityRole.All.Should().Be(union);
    }

    [Fact]
    public void ImpliedRoles_ForFullAdmin_IncludesMinorAdminAndPlayer()
    {
        var implied = SecurityRole.FullAdmin.ImpliedRoles;

        implied.Should().HaveFlag(SecurityRole.FullAdmin);
        implied.Should().HaveFlag(SecurityRole.MinorAdmin);
        implied.Should().HaveFlag(SecurityRole.Player);
    }

    [Fact]
    public void ImpliedRoles_ForFullBuilder_IncludesMinorBuilder()
    {
        var implied = SecurityRole.FullBuilder.ImpliedRoles;

        implied.Should().HaveFlag(SecurityRole.FullBuilder);
        implied.Should().HaveFlag(SecurityRole.MinorBuilder);
    }

    [Fact]
    public void ImpliedRoles_ForARoleWithNoHierarchy_IsJustItself()
    {
        SecurityRole.Helper.ImpliedRoles.Should().Be(SecurityRole.Helper);
    }

    [Theory]
    [InlineData(SecurityRole.FullAdmin, SecurityRole.MinorAdmin, true)]
    [InlineData(SecurityRole.FullAdmin, SecurityRole.Player, true)]
    [InlineData(SecurityRole.MinorAdmin, SecurityRole.FullAdmin, false)]
    [InlineData(SecurityRole.FullBuilder, SecurityRole.MinorBuilder, true)]
    [InlineData(SecurityRole.MinorBuilder, SecurityRole.FullBuilder, false)]
    public void Implies_ReflectsTheRoleHierarchy(SecurityRole role, SecurityRole other, bool expected)
    {
        role.Implies(other).Should().Be(expected);
    }
}
