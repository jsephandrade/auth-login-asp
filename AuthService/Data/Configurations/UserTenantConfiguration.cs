using AuthService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations;

public class UserTenantConfiguration : IEntityTypeConfiguration<UserTenant>
{
    public void Configure(EntityTypeBuilder<UserTenant> builder)
    {
        builder.ToTable("user_tenants");
        builder.HasKey(x => new { x.TenantId, x.UserId });
        builder.Property(x => x.TenantId).AsChar36();
        builder.Property(x => x.UserId).AsChar36();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne(x => x.Tenant).WithMany(t => t.Members).HasForeignKey(x => x.TenantId);
        builder.HasOne(x => x.User).WithMany(u => u.Tenants).HasForeignKey(x => x.UserId);

        builder.HasIndex(x => x.UserId);
    }
}
