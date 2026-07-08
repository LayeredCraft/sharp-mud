namespace SharpMud.Engine.Core;

// An account is an auth identity (docs/accounts-auth.md) outside the
// simulated world - not a Thing, unlike everything else in the game.
public readonly record struct AccountId(Guid Value)
{
    public static AccountId New() => new(Guid.CreateVersion7());
    public override string ToString() => Value.ToString();
}
