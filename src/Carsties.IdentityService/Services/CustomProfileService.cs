using System.Security.Claims;
using Carsties.IdentityService.Models;
using Duende.IdentityModel;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Identity;

namespace Carsties.IdentityService.Services;

public class CustomProfileService(UserManager<ApplicationUser> _userManager) : IProfileService
{
    private readonly UserManager<ApplicationUser> _userManager = _userManager;

    public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken ct)
    {
        var user =
            await _userManager.GetUserAsync(context.Subject)
            ?? throw new Exception("User not found");
        var existingClaims = await _userManager.GetClaimsAsync(user);
        var claims = new List<Claim> { new("username", user.UserName ?? user.Id) };

        context.IssuedClaims.AddRange(claims);

        var nameClaim = existingClaims.FirstOrDefault(x => x.Type == JwtClaimTypes.Name);
        if (nameClaim is not null)
        {
            context.IssuedClaims.Add(nameClaim);
        }
    }

    public Task IsActiveAsync(IsActiveContext context, CancellationToken ct)
    {
        return Task.CompletedTask;
    }
}
