using AuthService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_log");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).AsChar36();
        builder.Property(x => x.TenantId).AsChar36();
        builder.Property(x => x.ActorUserId).AsChar36();
        builder.Property(x => x.TargetUserId).AsChar36();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.Ip).HasColumnType("varbinary(16)");
        builder.Property(x => x.UserAgent).HasMaxLength(512);
        builder.Property(x => x.MetadataJson).HasColumnType("json");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
        builder.HasIndex(x => new { x.ActorUserId, x.CreatedAt });
    }
}
