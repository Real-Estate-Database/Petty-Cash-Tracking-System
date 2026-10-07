using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class Transaction
{
    public int TransactionId { get; set; }

    public int OrganizationId { get; set; }

    public int ReferenceNumber { get; set; }

    public int TransactionTypeId { get; set; }

    public int CategoryId { get; set; }

    public int? ParentTransactionId { get; set; }

    public int UserId { get; set; }

    public int? ApprovedByUserId { get; set; }

    public int? IssuedByUserId { get; set; }

    public int? ReconciledByUserId { get; set; }

    public decimal Amount { get; set; }

    public string Description { get; set; } = null!;

    public string Status { get; set; } = null!;

    public string RejectionReason { get; set; } = null!;

    public string? Notes { get; set; }

    public DateTime TransactionDate { get; set; }

    public DateTime? ApprovedDate { get; set; }

    public DateTime? ReconciliationDate { get; set; }

    public DateTime LastUpdatedDate { get; set; }

    public DateTime EntryDate { get; set; }

    public virtual User? ApprovedByUser { get; set; }

    public virtual AdvanceCategory Category { get; set; } = null!;

    public virtual ICollection<Transaction> InverseParentTransaction { get; set; } = new List<Transaction>();

    public virtual User? IssuedByUser { get; set; }

    public virtual Organization Organization { get; set; } = null!;

    public virtual Transaction? ParentTransaction { get; set; }

    public virtual ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();

    public virtual User? ReconciledByUser { get; set; }

    public virtual TransactionType TransactionType { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
