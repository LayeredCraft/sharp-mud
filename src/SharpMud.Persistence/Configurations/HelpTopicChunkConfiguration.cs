using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SharpMud.Engine.Help;

namespace SharpMud.Persistence.Configurations;

// HelpTopicChunk is a record (constructor-bound materialization, same
// mechanism ThingId already relies on for Thing.Id) - its own table with an
// explicit HelpTopicId FK, not an EF navigation collection off HelpTopic.
// HelpRepository loads/saves it manually, mirroring how ThingRepository
// handles the Behaviors table.
public sealed class HelpTopicChunkConfiguration : IEntityTypeConfiguration<HelpTopicChunk>
{
    public void Configure(EntityTypeBuilder<HelpTopicChunk> builder)
    {
        builder.ToTable("HelpTopicChunks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.HelpTopicId).HasConversion(id => id.Value, value => new HelpTopicId(value));
        builder.HasIndex(x => x.HelpTopicId);

        // A real FK constraint (no navigation property needed on either
        // side) - HelpRepository.SaveTopicAsync/DeleteTopicAsync already
        // delete/insert a topic and its chunks in the same
        // SaveChangesAsync batch, so EF's automatic dependency ordering
        // doesn't change either path's behavior. This makes the "explicit
        // HelpTopicId FK" claim in the comment above actually true at the
        // DB level, and cascades a delete so nothing outside
        // HelpRepository can orphan a topic's chunks - caught in PR
        // review.
        builder.HasOne<HelpTopic>()
            .WithMany()
            .HasForeignKey(x => x.HelpTopicId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(x => x.Text).IsRequired();
        builder.Property(x => x.EmbeddingModelId).IsRequired();
        builder.Property(x => x.SourceContentHash).IsRequired();

        // Stored as a BLOB (ADR-0010) rather than a delimited string - a
        // straight little-endian byte reinterpretation of the float[], not
        // meant to be portable across machine architectures, only read back
        // by this same process/DB. Factored into static methods (not inline
        // Span usage) because a ref struct value can't appear inside the
        // expression tree HasConversion compiles its lambdas into (CS8640).
        builder.Property(x => x.Embedding)
            .HasConversion(v => Serialize(v), v => Deserialize(v));
    }

    private static byte[] Serialize(float[] vector) => MemoryMarshal.AsBytes(vector.AsSpan()).ToArray();

    private static float[] Deserialize(byte[] bytes) => MemoryMarshal.Cast<byte, float>(bytes).ToArray();
}
