using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Help;

namespace SharpMud.Persistence.Configurations;

// Aliases/Keywords are List<string>-backed IReadOnlyList<string> properties
// with no public setter (see HelpTopic) - EF binds directly to the backing
// field (PropertyAccessMode.Field) instead of requiring a settable property,
// same "expose IReadOnlyList, mutate via a named method" shape
// coding-standards.md requires for collections on domain entities.
public sealed class HelpTopicConfiguration : IEntityTypeConfiguration<HelpTopic>
{
    // ASCII "unit separator" (0x1F) - won't collide with real alias/keyword text.
    private const char Separator = '\u001F';

    public void Configure(EntityTypeBuilder<HelpTopic> builder)
    {
        builder.ToTable("HelpTopics");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasConversion(id => id.Value, value => new HelpTopicId(value));
        builder.Property(x => x.Key).IsRequired();

        // Guards the check-then-act gap in HelpTopicEditCommand
        // (FindByNameOrAliasAsync -> create-if-null -> SaveTopicAsync, no
        // transaction spanning it) - without this, two concurrent
        // `helptopic newtopic ...` calls for the same new key could both
        // observe null and each insert a row, leaving FindByNameOrAliasAsync's
        // FirstOrDefault to pick one arbitrarily. Low-probability today
        // (solo/small-collaborator project), but cheap - caught in PR
        // review.
        builder.HasIndex(x => x.Key).IsUnique();
        builder.Property(x => x.Category).IsRequired();
        builder.Property(x => x.Body).IsRequired();
        builder.Property(x => x.UpdatedAtUtc).IsRequired();

        builder.Property(x => x.Aliases)
            .HasConversion(v => Join(v), v => Split(v))
            .Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        builder.Property(x => x.Keywords)
            .HasConversion(v => Join(v), v => Split(v))
            .Metadata.SetPropertyAccessMode(PropertyAccessMode.Field);

        // HelpTopicChunk is its own table with an explicit HelpTopicId FK,
        // loaded/saved manually by HelpRepository (mirrors ThingRepository's
        // Behaviors handling) rather than an EF navigation collection.
        builder.Ignore(x => x.Chunks);
        builder.Ignore(x => x.ContentHash); // derived from Body, not stored
    }

    // Factored out of the HasConversion lambdas above - a collection
    // expression ([]) can't appear inside an expression tree (CS9175),
    // which is what EF compiles a HasConversion lambda into.
    private static string Join(IReadOnlyList<string> values) => string.Join(Separator, values);

    private static List<string> Split(string value) =>
        value.Length == 0 ? new List<string>() : value.Split(Separator, StringSplitOptions.RemoveEmptyEntries).ToList();
}
