using System.ComponentModel.DataAnnotations;
using PettyCashDatabase.Shared.Auth;

namespace PettyCashDatabase.Shared.DTOs.Auth;

public sealed class TenantOnboardingDto
{
    // Step 1 — Organization
    [Required]
    [StringLength(255)]
    [Display(Name = "OrganizationName")]
    public string OrganizationName { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Address")]
    public string? Address { get; set; }

    [Required]
    [Display(Name = "Country")]
    [Range(1, int.MaxValue, ErrorMessage = "Country is required.")]
    public int OrganizationCountryId { get; set; }

    [Display(Name = "Region")]
    public int? OrganizationRegionId { get; set; }

    [Display(Name = "District")]
    public int? OrganizationDistrictId { get; set; }

    [Required]
    [EmailAddress]
    [StringLength(255)]
    [Display(Name = "Email")]
    public string OrganizationEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "Telephone")]
    public string OrganizationTelephone { get; set; } = string.Empty;

    // Step 2 — Admin
    [Required]
    [StringLength(50)]
    [Display(Name = "FirstName")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "LastName")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(255)]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    [PasswordPolicy(EmailProperty = nameof(Email))]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    [StringLength(50)]
    [Display(Name = "TelephoneNumber")]
    public string TelephoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [Display(Name = "Time zone")]
    public string TimeZoneId { get; set; } = TimeZoneCatalog.Default.Id;

    // Kept for compatibility with older clients and database rows.
    public short? TimeZone { get; set; }
}
