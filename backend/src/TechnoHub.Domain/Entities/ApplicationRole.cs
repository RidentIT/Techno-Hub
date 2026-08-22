using Microsoft.AspNetCore.Identity;

namespace TechnoHub.Domain.Entities;

/// <summary>
/// One of the three fixed staff roles. Roles are coarse-grained; the fine-grained
/// permissions live in <see cref="UserScope"/>.
/// </summary>
public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName) : base(roleName)
    {
    }

    public string? Description { get; set; }
}
