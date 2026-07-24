namespace SharpMud.Engine.Help;

/// <summary>One <see cref="IHelpSearchIndex.SearchAsync"/> result - a matched topic and its relevance score.</summary>
/// <param name="Topic">The matched topic.</param>
/// <param name="Score">Cosine similarity in <c>[-1, 1]</c> (in practice <c>[0, 1]</c> for non-negative embeddings) between the query and this topic's best-matching chunk.</param>
public sealed record HelpSearchHit(HelpTopic Topic, double Score);
