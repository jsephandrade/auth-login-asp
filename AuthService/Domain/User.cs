using System.ComponentModel.DataAnnotations;

namespace AuthService.Domain;

public class User
{
    public Guid Id { get; set; }

    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(320)]
    public string EmailNormalized { get; set; } = string.Empty;

    [MaxLength(255)]
    public string PasswordHash { get; set; } = string.Empty;

    public DateTime? EmailVerifiedAt { get; set; }

    public UserStatus Status { get; set; } = UserStatus.Active;

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<UserTenant> Tenants { get; set; } = new List<UserTenant>();
    public ICollection<UserRole> Roles { get; set; } = new List<UserRole>();
    public ICollection<Session> Sessions { get; set; } = new List<Session>();
}
