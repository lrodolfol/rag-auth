using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RagAuth.Api.Domain.Entities;

namespace RagAuth.Api.Infrastructure.Persistence.Configurations;

public sealed class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("businesses");
        builder.HasKey(business => business.Id);
        builder.Property(business => business.Name).HasMaxLength(150).IsRequired();
        builder.Property(business => business.Description).HasMaxLength(1000).IsRequired();
        builder.Property(business => business.Email).HasMaxLength(254);
        builder.Property(business => business.Telephone).HasMaxLength(11);
        builder.Property(business => business.Address).HasMaxLength(300);
        builder.Property(business => business.Details).HasMaxLength(2000);
        builder.HasIndex(business => business.ClientId).IsUnique().HasDatabaseName(UniqueIndexNames.BusinessClientId);
        builder.HasQueryFilter(business => business.Client.DeletedAt == null);
    }
}
