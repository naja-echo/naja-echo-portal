using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NajaEcho.Domain.Blueprints;

namespace NajaEcho.Infrastructure.Persistence.Configurations;

public sealed class UserBlueprintConfiguration : IEntityTypeConfiguration<UserBlueprint>
{
    public void Configure(EntityTypeBuilder<UserBlueprint> builder)
    {
        builder.ToTable("user_blueprints");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(u => u.BlueprintId).HasColumnName("blueprint_id").IsRequired();
        builder.Property(u => u.AddedAt).HasColumnName("added_at").IsRequired();

        builder.HasIndex(u => new { u.UserId, u.BlueprintId })
            .IsUnique()
            .HasDatabaseName("ux_user_blueprints_user_blueprint");

        builder.HasIndex(u => u.UserId).HasDatabaseName("ix_user_blueprints_user_id");

        builder.HasOne<CraftingBlueprint>()
            .WithMany()
            .HasForeignKey(u => u.BlueprintId)
            .HasConstraintName("fk_user_blueprints_blueprint_id")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
