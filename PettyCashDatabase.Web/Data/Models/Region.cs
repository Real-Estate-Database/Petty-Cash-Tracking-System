using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class Region
{
    public int RegionId { get; set; }

    public string? RegionName { get; set; }

    public int? Country { get; set; }

    public string? State { get; set; }

    public virtual Country? CountryNavigation { get; set; }

    public virtual ICollection<District> Districts { get; set; } = new List<District>();

    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
