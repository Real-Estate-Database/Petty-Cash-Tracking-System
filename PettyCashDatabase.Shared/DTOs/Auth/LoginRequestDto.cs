using System.ComponentModel.DataAnnotations;

namespace PettyCashDatabase.Shared.DTOs.Auth;

public sealed class LoginRequestDto
{
    [Required]
    [StringLength(255)]
    [Display(Name = "Email or Telephone Number")]
    public string Identifier { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;
}
