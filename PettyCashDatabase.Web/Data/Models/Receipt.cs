using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class Receipt
{
    public int ReceiptId { get; set; }

    public int UserId { get; set; }

    public int TransactionId { get; set; }

    public string ReceiptNumber { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string? ContentType { get; set; }

    public int FileSize { get; set; }

    public DateTime EntryDate { get; set; }

    public string AmountInWords { get; set; } = null!;

    public virtual Transaction Transaction { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
