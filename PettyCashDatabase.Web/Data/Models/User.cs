using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class User
{
    public int UserId { get; set; }

    public int OrganizationId { get; set; }

    public int? RoleId { get; set; }

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string TelephoneNumber { get; set; } = null!;

    public bool IsActive { get; set; }

    public bool Disabled { get; set; }

    public bool Deleted { get; set; }

    public string? Image { get; set; }

    public DateTime? LastLoginDate { get; set; }

    public int? CountryId { get; set; }

    public int? RegionId { get; set; }

    public int DistrictId { get; set; }

    public DateTime EntryDate { get; set; }

    public short? TimeZone { get; set; }

    public virtual ICollection<AuditTrail> AuditTrails { get; set; } = new List<AuditTrail>();

    public virtual Country? Country { get; set; }

    public virtual District District { get; set; } = null!;

    public virtual Organization Organization { get; set; } = null!;

    public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

    public virtual Region? Region { get; set; }

    public virtual Role? Role { get; set; }

    public virtual ICollection<Transaction> TransactionApprovedByUsers { get; set; } = new List<Transaction>();

    public virtual ICollection<Transaction> TransactionIssuedByUsers { get; set; } = new List<Transaction>();

    public virtual ICollection<Transaction> TransactionReconciledByUsers { get; set; } = new List<Transaction>();

    public virtual ICollection<Transaction> TransactionUsers { get; set; } = new List<Transaction>();

    public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}
