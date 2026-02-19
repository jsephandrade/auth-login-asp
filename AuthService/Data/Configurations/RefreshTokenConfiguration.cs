using AuthService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).AsChar36();
        builder.Property(x => x.SessionId).AsChar36();
        builder.Property(x => x.TenantId).AsChar36();
        builder.Property(x => x.UserId).AsChar36();
        builder.Property(x => x.RotatedFromId).AsChar36();
        builder.Property(x => x.TokenHash).HasColumnType("binary(32)").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.Property(x => x.RevokedAt);
        builder.Property(x => x.Ip).HasColumnType("varbinary(16)");
        builder.Property(x => x.UserAgentHash).HasColumnType("binary(32)");

        builder.HasOne(x => x.Session).WithMany(s => s.RefreshTokens).HasForeignKey(x => x.SessionId);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => x.SessionId);
    }
}
