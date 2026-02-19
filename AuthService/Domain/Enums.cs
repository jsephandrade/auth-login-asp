namespace AuthService.Domain;

public enum TenantStatus
{
    Active = 1,
    Suspended = 2,
    Deleted = 3
}

public enum UserStatus
{
    Active = 1,
    Locked = 2,
    Deleted = 3
}

public enum MembershipStatus
{
    Active = 1,
    Invited = 2,
    Suspended = 3
}
