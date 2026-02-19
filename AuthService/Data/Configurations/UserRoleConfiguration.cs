using AuthService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations;

public class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> builder)
    {
        builder.ToTable("user_roles");
        builder.HasKey(x => new { x.TenantId, x.UserId, x.RoleId });
        builder.Property(x => x.TenantId).AsChar36();
        builder.Property(x => x.UserId).AsChar36();
        builder.Property(x => x.RoleId).AsChar36();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Role).WithMany(r => r.UserRoles).HasForeignKey(x => x.RoleId);

        builder.HasIndex(x => x.UserId);
    }
}
