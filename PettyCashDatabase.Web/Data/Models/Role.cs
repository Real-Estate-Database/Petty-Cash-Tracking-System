using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class Role
{
    public int RoleId { get; set; }

    public int OrganizationId { get; set; }

    public string RoleName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public bool IsSystemRole { get; set; }

    public bool Disabled { get; set; }

    public DateTime EntryDate { get; set; }

    public bool Removed { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
