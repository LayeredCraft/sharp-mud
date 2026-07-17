using SharpMud.Engine.Behaviors;
using SharpMud.Engine.Commands;
using SharpMud.Engine.Commands.Builtin;
using SharpMud.Engine.Core;
using SharpMud.Engine.Sessions;

namespace SharpMud.Host;

// Shared by every transport (local CLI, Telnet, ...) so adding a transport
// never touches game logic - just a new ISession implementation feeding the
// same loop (SPEC.md transport-agnostic sessions decision).
public static class SessionLoop
{
    public static async Task RunAsync(
        World world,
        ICommandParser parser,
        ICommandRegistry registry,
        ISession session,
        Thing player,
        IThingRepository repository,
        CancellationToken ct)
    {
        // "quit" disconnects intentionally (QuitCommand) - that path skips
        // the Linkdead grace period entirely and removes the player
        // immediately below, same as every disconnect used to behave before
        // ADR-0004. Any other way the loop ends (dropped connection, server
        // shutdown) goes Linkdead instead, so LoginFlow can reconnect it.
        var explicitQuit = false;

        try
        {
            await session.WriteLineAsync("Welcome to SharpMud.", ct);
            await session.WriteLineAsync(string.Empty, ct);

            // player.Parent, not a fixed "startingRoom" - a reloaded/
            // reconnecting player (docs/persistence.md) may not be in the
            // hub's starting room.
            if (player.Parent is { } room)
                await LookCommand.SendRoomDescriptionAsync(player, room, ct);

            while (session.IsConnected && !ct.IsCancellationRequested)
            {
                await session.WriteAsync("> ", ct);

                var input = await session.ReadLineAsync(ct);
                if (input is null)
                    break;

                var parsed = parser.Parse(input);
                if (parsed.Verb.Length == 0)
                    continue;

                var currentRoom = player.Parent;
                if (currentRoom is null)
                    break;

                if (!registry.TryResolve(parsed.Verb, out var command))
                {
                    await session.WriteLineAsync("Huh?", ct);
                    continue;
                }

                if (parsed.Verb.Equals("quit", StringComparison.OrdinalIgnoreCase))
                    explicitQuit = true;

                var context = new CommandContext(player, currentRoom, parsed.Args, world, session);
                await command.ExecuteAsync(context, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown mid-read/write - fall through to the
            // save-on-disconnect below rather than losing this player's
            // state, which is the whole point of a finally-guaranteed save.
        }
        finally
        {
            var playerBehavior = player.FindBehavior<PlayerBehavior>();

            // Guard + do this BEFORE the awaited save below (PR #1 review) -
            // if a reconnect races in while this disconnect is being
            // processed, LoginFlow must see Linkdead immediately, not a
            // stale Playing state for as long as SaveTreeAsync takes. The
            // playerBehavior.Session == session check additionally backs off
            // entirely if a newer session already took over this same Thing
            // by the time we get here, so this disconnect can't clobber an
            // already-active reconnect. explicitQuit's removal deliberately
            // does NOT happen here (see below) - only the Linkdead
            // transition needs to race ahead of the save.
            if (!explicitQuit && playerBehavior?.Session == session)
            {
                // Linkdead, not an immediate world removal (ADR-0004) - the
                // Thing stays live in its room so LoginFlow can reconnect a
                // new session to it within ReconnectPolicy.GraceWindow.
                // LinkdeadSweeper finishes the removal once that window
                // elapses without a reconnect.
                playerBehavior.EnterLinkdead(DateTimeOffset.UtcNow);
            }

            // CancellationToken.None, not ct - a graceful shutdown cancels ct
            // first and THEN reaches this save; using ct here would abort
            // the save at exactly the moment it matters most. The try/catch/
            // finally around the whole method (not just this line)
            // guarantees this runs even if a read/write above threw due to
            // cancellation mid-operation.
            await repository.SaveTreeAsync(player, CancellationToken.None);

            // explicitQuit's removal happens AFTER the save, not before
            // (PR #1 review) - ThingRepository.SaveTreeAsync persists
            // ParentId from thing.Parent at save time, so removing first
            // would save ParentId=null and lose the room a player quit
            // from. Same session-identity guard as above, for consistency.
            if (explicitQuit && playerBehavior?.Session == session)
            {
                player.Parent?.Remove(player);
                world.Unregister(player.Id);
            }
        }
    }
}
