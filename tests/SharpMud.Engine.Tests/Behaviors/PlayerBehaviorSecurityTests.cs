using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;

namespace SharpMud.Engine.Tests.Behaviors;

public sealed class PlayerBehaviorSecurityTests
{
    private static PlayerBehavior MakePlayer() => new() { Username = "TestUser", PasswordHash = "test-hash" };

    [Fact]
    public void Roles_DefaultsToPlayer_ForANewCharacter()
    {
        var player = MakePlayer();

        player.Roles.Should().Be(SecurityRole.Player);
    }

    [Fact]
    public void GrantRole_FullAdmin_AlsoGrantsMinorAdminAndPlayer()
    {
        var player = MakePlayer();

        player.GrantRole(SecurityRole.FullAdmin);

        player.Roles.Should().HaveFlag(SecurityRole.FullAdmin);
        player.Roles.Should().HaveFlag(SecurityRole.MinorAdmin);
        player.Roles.Should().HaveFlag(SecurityRole.Player);
    }

    [Fact]
    public void GrantRole_FullBuilder_AlsoGrantsMinorBuilderAndPlayer()
    {
        var player = MakePlayer();

        player.GrantRole(SecurityRole.FullBuilder);

        player.Roles.Should().HaveFlag(SecurityRole.FullBuilder);
        player.Roles.Should().HaveFlag(SecurityRole.MinorBuilder);
        player.Roles.Should().HaveFlag(SecurityRole.Player);
    }

    [Fact]
    public void GrantRole_FullBuilder_DoesNotGrantAnyAdminRole()
    {
        var player = MakePlayer();

        player.GrantRole(SecurityRole.FullBuilder);

        player.Roles.Should().NotHaveFlag(SecurityRole.MinorAdmin);
        player.Roles.Should().NotHaveFlag(SecurityRole.FullAdmin);
    }

    [Fact]
    public void GrantRole_IsIdempotent()
    {
        var player = MakePlayer();
        player.GrantRole(SecurityRole.FullAdmin);

        var act = () => player.GrantRole(SecurityRole.FullAdmin);

        act.Should().NotThrow();
        player.Roles.Should().HaveFlag(SecurityRole.FullAdmin);
    }

    [Fact]
    public void RevokeRole_MinorAdmin_ReturnsFailureAndLeavesRolesUnchanged_WhenActorAlsoHoldsFullAdmin()
    {
        var player = MakePlayer();
        player.GrantRole(SecurityRole.FullAdmin);
        var rolesBefore = player.Roles;

        var result = player.RevokeRole(SecurityRole.MinorAdmin);

        result.Should().NotBeNull();
        player.Roles.Should().Be(rolesBefore);
    }

    [Fact]
    public void RevokeRole_FullAdmin_SucceedsAndLeavesMinorAdminAndPlayerIntact_WhenActorHoldsFullAdmin()
    {
        var player = MakePlayer();
        player.GrantRole(SecurityRole.FullAdmin);

        var result = player.RevokeRole(SecurityRole.FullAdmin);

        result.Should().BeNull();
        player.Roles.Should().NotHaveFlag(SecurityRole.FullAdmin);
        player.Roles.Should().HaveFlag(SecurityRole.MinorAdmin);
        player.Roles.Should().HaveFlag(SecurityRole.Player);
    }

    [Fact]
    public void RevokeRole_MinorAdmin_Succeeds_WhenActorDoesNotAlsoHoldFullAdmin()
    {
        var player = MakePlayer();
        player.GrantRole(SecurityRole.MinorAdmin);

        var result = player.RevokeRole(SecurityRole.MinorAdmin);

        result.Should().BeNull();
        player.Roles.Should().NotHaveFlag(SecurityRole.MinorAdmin);
    }

    [Fact]
    public void RevokeRole_Player_ReturnsFailureAndLeavesRolesUnchanged_WhenActorAlsoHoldsFullBuilder()
    {
        var player = MakePlayer();
        player.GrantRole(SecurityRole.FullBuilder);
        var rolesBefore = player.Roles;

        var result = player.RevokeRole(SecurityRole.Player);

        result.Should().NotBeNull();
        player.Roles.Should().Be(rolesBefore);
    }

    [Fact]
    public void RevokeRole_FullBuilder_SucceedsAndLeavesMinorBuilderAndPlayerIntact_WhenActorHoldsFullBuilder()
    {
        var player = MakePlayer();
        player.GrantRole(SecurityRole.FullBuilder);

        var result = player.RevokeRole(SecurityRole.FullBuilder);

        result.Should().BeNull();
        player.Roles.Should().NotHaveFlag(SecurityRole.FullBuilder);
        player.Roles.Should().HaveFlag(SecurityRole.MinorBuilder);
        player.Roles.Should().HaveFlag(SecurityRole.Player);
    }

    [Fact]
    public void Mute_SetsIsMuted_AndUnmute_ClearsIt()
    {
        var player = MakePlayer();

        player.Mute();
        player.IsMuted.Should().BeTrue();

        player.Unmute();
        player.IsMuted.Should().BeFalse();
    }

    [Fact]
    public void Ban_SetsIsBanned_AndUnban_ClearsIt()
    {
        var player = MakePlayer();

        player.Ban();
        player.IsBanned.Should().BeTrue();

        player.Unban();
        player.IsBanned.Should().BeFalse();
    }

    [Fact]
    public void MarkBooted_SetsWasBooted()
    {
        var player = MakePlayer();

        player.MarkBooted();

        player.WasBooted.Should().BeTrue();
    }
}
