using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Data.Configurations;

public static class ConfigurationHelpers
{
    public static PropertyBuilder<Guid> AsChar36(this PropertyBuilder<Guid> builder)
        => builder.HasColumnType("char(36)");

    public static PropertyBuilder<Guid?> AsChar36(this PropertyBuilder<Guid?> builder)
        => builder.HasColumnType("char(36)");
}
