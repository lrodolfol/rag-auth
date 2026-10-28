using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Configurations;

public sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(client => client.Id);
        builder.Property(client => client.AccessCode).HasMaxLength(12).IsRequired();
        builder.Property(client => client.Name).HasMaxLength(150).IsRequired();
        builder.Property(client => client.Email).HasMaxLength(254).IsRequired();
        builder.Property(client => client.PasswordHash).HasColumnType("text").IsRequired();
        builder.HasIndex(client => client.AccessCode).IsUnique().HasDatabaseName(UniqueIndexNames.ClientAccessCode);
        builder.HasIndex(client => client.Email)
            .IsUnique()
            .HasFilter("\"deletedAt\" IS NULL")
            .HasDatabaseName(UniqueIndexNames.ActiveClientEmail);
        builder.HasQueryFilter(client => client.DeletedAt == null);
        builder.HasOne(client => client.Business)
            .WithOne(business => business.Client)
            .HasForeignKey<Business>(business => business.ClientId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
