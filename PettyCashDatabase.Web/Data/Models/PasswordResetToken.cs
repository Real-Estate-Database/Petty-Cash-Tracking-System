namespace PettyCashDatabase.Web.Data.Models;

public partial class PasswordResetToken
{
    public int PasswordResetTokenId { get; set; }

    public int UserId { get; set; }

    public string TokenHash { get; set; } = null!;

    public DateTime ExpiredDate { get; set; }

    public DateTime? UsedDate { get; set; }

    public DateTime EntryDate { get; set; }

    public virtual User User { get; set; } = null!;
}
