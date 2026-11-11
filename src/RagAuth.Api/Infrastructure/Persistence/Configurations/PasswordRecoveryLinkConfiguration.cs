using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Configurations;

public sealed class PasswordRecoveryLinkConfiguration : IEntityTypeConfiguration<PasswordRecoveryLink>
{
    public const string TableName = "password_recovery_links";
    public const string ClientIssuedIndexName = "ix_password_recovery_links_client_issued";
    public const string ActiveLinkFilter = "\"usedAt\" IS NULL AND \"revokedAt\" IS NULL";

    public void Configure(EntityTypeBuilder<PasswordRecoveryLink> builder)
    {
        builder.ToTable(TableName);
        builder.HasKey(link => link.Id);
        ConfigureColumns(builder);
        ConfigureIndexes(builder);
        builder.HasOne(link => link.Client)
            .WithMany(client => client.PasswordRecoveryLinks)
            .HasForeignKey(link => link.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasQueryFilter(link => link.Client.DeletedAt == null);
    }

    private static void ConfigureColumns(EntityTypeBuilder<PasswordRecoveryLink> builder)
    {
        builder.Property(link => link.Id).UseIdentityByDefaultColumn();
        builder.Property(link => link.Selector).HasColumnType("varchar(22)").IsRequired();
        builder.Property(link => link.ValidatorHash).HasColumnType("char(64)").IsRequired();
        builder.Property(link => link.Purpose).HasColumnType("varchar(32)").IsRequired();
        builder.Property(link => link.IssuedAt).IsRequired();
        builder.Property(link => link.ExpiresAt).IsRequired();
    }

    private static void ConfigureIndexes(EntityTypeBuilder<PasswordRecoveryLink> builder)
    {
        builder.HasIndex(link => link.Selector)
            .IsUnique()
            .HasDatabaseName(UniqueIndexNames.PasswordRecoveryLinkSelector);
        builder.HasIndex(link => link.ClientId)
            .IsUnique()
            .HasFilter(ActiveLinkFilter)
            .HasDatabaseName(UniqueIndexNames.ActivePasswordRecoveryLinkPerClient);
        builder.HasIndex(link => new { link.ClientId, link.IssuedAt })
            .HasDatabaseName(ClientIssuedIndexName);
    }
}
