using SharpMud.Engine.Core;

namespace SharpMud.Engine.Commands.Builtin.Admin;

/// <summary>
/// Registers the moderation command set (ADR-0005) - day-to-day moderation
/// (<c>boot</c>/<c>mute</c>/<c>unmute</c>/<c>announce</c>) at <see
/// cref="SecurityRole.MinorAdmin"/>, harder-to-reverse or
/// privilege-affecting actions (<c>ban</c>/<c>unban</c>/<c>rolegrant</c>/
/// <c>rolerevoke</c>) at <see cref="SecurityRole.FullAdmin"/>. Not called
/// automatically by <see cref="BuiltinCommands.RegisterAll"/> - a consumer
/// calls this themselves (passing their own <see cref="IThingRepository"/>)
/// from whichever callback they already pass into their ruleset's
/// registration entry point, the same way a consumer opts into any other
/// non-default command set.
/// </summary>
public static class AdminCommands
{
    public static void RegisterAll(ICommandRegistry registry, IThingRepository repository)
    {
        registry.RegisterWithRole(new BootCommand(), SecurityRole.MinorAdmin);
        registry.RegisterWithRole(new MuteCommand(repository), SecurityRole.MinorAdmin);
        registry.RegisterWithRole(new UnmuteCommand(repository), SecurityRole.MinorAdmin);
        registry.RegisterWithRole(new AnnounceCommand(), SecurityRole.MinorAdmin);

        registry.RegisterWithRole(new BanCommand(repository), SecurityRole.FullAdmin);
        registry.RegisterWithRole(new UnbanCommand(repository), SecurityRole.FullAdmin);
        registry.RegisterWithRole(new RoleGrantCommand(repository), SecurityRole.FullAdmin);
        registry.RegisterWithRole(new RoleRevokeCommand(repository), SecurityRole.FullAdmin);
    }
}
