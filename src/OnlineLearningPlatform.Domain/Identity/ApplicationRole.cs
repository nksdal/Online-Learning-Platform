using Microsoft.AspNetCore.Identity;

namespace OnlineLearningPlatform.Domain.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName)
        : base(roleName)
    {
    }
}
