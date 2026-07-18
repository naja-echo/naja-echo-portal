using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Loot;
using NajaEcho.Infrastructure.Identity;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class LootLedgerEntryConfiguration : IEntityTypeConfiguration<LootLedgerEntry>
{
    public void Configure(EntityTypeBuilder<LootLedgerEntry> builder)
    {
        builder.ToTable("loot_ledger", t =>
        {
            t.HasCheckConstraint("ck_loot_ledger_kind", "kind IN ('OrgPoints', 'LootPoints')");
            t.HasCheckConstraint("ck_loot_ledger_reason", "length(btrim(reason)) >= 1");
        });

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.MemberId).HasColumnName("member_id").IsRequired();
        builder.Property(l => l.Kind).HasColumnName("kind").IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);
        builder.Property(l => l.Amount).HasColumnName("amount").IsRequired();
        builder.Property(l => l.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
        builder.Property(l => l.ActorId).HasColumnName("actor_id").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.MemberId)
            .HasConstraintName("fk_loot_ledger_member_id")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.ActorId)
            .HasConstraintName("fk_loot_ledger_actor_id")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(l => new { l.MemberId, l.Kind, l.CreatedAt })
            .HasDatabaseName("ix_loot_ledger_member_kind_created")
            .IsDescending(false, false, true);
    }
}
