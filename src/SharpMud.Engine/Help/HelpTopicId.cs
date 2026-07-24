namespace SharpMud.Engine.Help;

/// <summary>Stable identity for a <see cref="HelpTopic"/> - same wrapper-struct shape as <c>ThingId</c>.</summary>
public readonly record struct HelpTopicId(Guid Value)
{
    /// <summary>Generates a new, time-ordered id.</summary>
    public static HelpTopicId New() => new(Guid.CreateVersion7());

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}
