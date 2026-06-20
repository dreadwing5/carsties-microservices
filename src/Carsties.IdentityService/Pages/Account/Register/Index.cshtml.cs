using Carsties.IdentityService.Models;
using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Carsties.IdentityService.Pages.Account.Register;

[SecurityHeaders]
[AllowAnonymous]
public class Index(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IIdentityServerInteractionService interaction,
    IEventService events
) : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly IIdentityServerInteractionService _interaction = interaction;
    private readonly IEventService _events = events;

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public void OnGet(string? returnUrl)
    {
        Input = new InputModel { ReturnUrl = returnUrl };
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = new ApplicationUser { UserName = Input.Username, Email = Input.Email };
        var result = await _userManager.CreateAsync(user, Input.Password!);

        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await _signInManager.SignInAsync(user, isPersistent: false);

        var context = await _interaction.GetAuthorizationContextAsync(Input.ReturnUrl, ct);
        await _events.RaiseAsync(
            new UserLoginSuccessEvent(
                user.UserName,
                user.Id,
                user.UserName,
                clientId: context?.Client.ClientId
            ),
            ct
        );
        Telemetry.Metrics.UserLogin(
            context?.Client.ClientId,
            IdentityServerConstants.LocalIdentityProvider
        );

        if (context != null)
        {
            ArgumentNullException.ThrowIfNull(Input.ReturnUrl, nameof(Input.ReturnUrl));

            if (context.IsNativeClient())
            {
                return this.LoadingPage(Input.ReturnUrl);
            }

            return Redirect(Input.ReturnUrl);
        }

        if (Url.IsLocalUrl(Input.ReturnUrl))
        {
            return Redirect(Input.ReturnUrl);
        }

        if (string.IsNullOrEmpty(Input.ReturnUrl))
        {
            return Redirect("~/");
        }

        throw new ArgumentException("invalid return URL");
    }
}
