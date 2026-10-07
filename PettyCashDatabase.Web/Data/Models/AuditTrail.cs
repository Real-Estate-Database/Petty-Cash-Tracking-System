using System;
using System.Collections.Generic;

namespace PettyCashDatabase.Web.Data.Models;

public partial class AuditTrail
{
    public int AuditTrailId { get; set; }

    public int UserId { get; set; }

    public string AccessTokenHash { get; set; } = null!;

    public string RefreshTokenHash { get; set; } = null!;

    public string? ClientType { get; set; }

    public string? DeviceIdentifier { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public int? CountryId { get; set; }

    public DateTime EntryDate { get; set; }

    public DateTime ExpiryDate { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime? RevokedDate { get; set; }

    public string Description { get; set; } = null!;

    public virtual Country? Country { get; set; }

    public virtual User User { get; set; } = null!;
}
