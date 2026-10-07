using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class AdvanceCategory
{
    public int CategoryId { get; set; }

    public int OrganizationId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool Disabled { get; set; }

    public DateTime EntryDate { get; set; }

    public DateTime LastUpdated { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
