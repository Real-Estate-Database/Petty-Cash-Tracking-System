using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class Country
{
    public int Id { get; set; }

    public string? Code { get; set; }

    public string? CountryName { get; set; }

    public bool Enabled { get; set; }

    public virtual ICollection<AuditTrail> AuditTrails { get; set; } = new List<AuditTrail>();

    public virtual ICollection<District> Districts { get; set; } = new List<District>();

    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();

    public virtual ICollection<Region> Regions { get; set; } = new List<Region>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
