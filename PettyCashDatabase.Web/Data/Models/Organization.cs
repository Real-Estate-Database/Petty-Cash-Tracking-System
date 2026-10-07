using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class Organization
{
    public int OrganizationId { get; set; }

    public string OrganizationName { get; set; } = null!;

    public bool Disabled { get; set; }

    public bool Deleted { get; set; }

    public string? Logo { get; set; }

    public string? Address { get; set; }

    public int CountryId { get; set; }

    public int? RegionId { get; set; }

    public int DistrictId { get; set; }

    public string Email { get; set; } = null!;

    public string Telephone { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<AdvanceCategory> AdvanceCategories { get; set; } = new List<AdvanceCategory>();

    public virtual Country Country { get; set; } = null!;

    public virtual District District { get; set; } = null!;

    public virtual Region? Region { get; set; }

    public virtual ICollection<Role> Roles { get; set; } = new List<Role>();

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
