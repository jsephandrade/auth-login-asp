using AuthService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).AsChar36();
        builder.Property(x => x.TenantId).AsChar36();
        builder.Property(x => x.UserId).AsChar36();
        builder.Property(x => x.SessionVersion).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.LastSeenAt);
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.CompromisedAt);
        builder.Property(x => x.Ip).HasColumnType("varbinary(16)");
        builder.Property(x => x.UserAgentHash).HasColumnType("binary(32)");

        builder.HasOne(x => x.User).WithMany(u => u.Sessions).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.TenantId);
        builder.HasIndex(x => new { x.UserId, x.RevokedAt });
    }
}
