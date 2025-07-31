using Microsoft.AspNetCore.Identity;

namespace dymaptic.Blazor.StateManagement.Server;

public class ApplicationUser: IdentityUser<Guid>
{
    
}

public class ApplicationRole: IdentityRole<Guid>
{
    public ApplicationRole()
    {
    }

    public ApplicationRole(string roleName): base(roleName)
    {
    }
}