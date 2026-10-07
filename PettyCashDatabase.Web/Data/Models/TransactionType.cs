using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class TransactionType
{
    public int TransactionTypeId { get; set; }

    public string TransactionTypeName { get; set; } = null!;

    public string Description { get; set; } = null!;

    public DateTime EntryDate { get; set; }

    public virtual ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
