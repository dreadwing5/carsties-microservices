using System.ComponentModel.DataAnnotations;

namespace Carsties.IdentityService.Pages.Account.Register;

public class InputModel
{
    [Required]
    [Display(Name = "Username")]
    public string? Username { get; set; }

    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Required]
    [DataType(DataType.Password)]
    [Compare(
        nameof(Password),
        ErrorMessage = "The password and confirmation password do not match."
    )]
    [Display(Name = "Confirm password")]
    public string? ConfirmPassword { get; set; }

    public string? ReturnUrl { get; set; }
}
