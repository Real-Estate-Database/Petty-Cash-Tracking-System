using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class District
{
    public int DistrictId { get; set; }

    public string? DistrictName { get; set; }

    public int? Country { get; set; }

    public string? State { get; set; }

    public int? Region { get; set; }

    public string? TempDistrict { get; set; }

    public int? Position { get; set; }

    public virtual Country? CountryNavigation { get; set; }

    public virtual ICollection<Organization> Organizations { get; set; } = new List<Organization>();

    public virtual Region? RegionNavigation { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
