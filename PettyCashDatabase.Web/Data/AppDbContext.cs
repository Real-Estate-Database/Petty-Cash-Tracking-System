using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using PettyCashDatabase.Web.Data.Models;

namespace PettyCashDatabase.Web.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdvanceCategory> AdvanceCategories { get; set; }

    public virtual DbSet<AuditTrail> AuditTrails { get; set; }

    public virtual DbSet<Country> Countries { get; set; }

    public virtual DbSet<District> Districts { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<Permission> Permissions { get; set; }

    public virtual DbSet<Receipt> Receipts { get; set; }

    public virtual DbSet<Region> Regions { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Transaction> Transactions { get; set; }

    public virtual DbSet<TransactionType> TransactionTypes { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserPermission> UserPermissions { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdvanceCategory>(builder
            =>
        {
            builder.HasKey(AdvanceCategory => AdvanceCategory.CategoryId);

            builder.Property(AdvanceCategory => AdvanceCategory.Description).HasMaxLength(500);
            builder.Property(AdvanceCategory => AdvanceCategory.EntryDate)
                .HasDefaultValueSql("(getutcdate())", "DF__AdvanceCa__Entry__72C60C4A")
                .HasColumnType("datetime");
            builder.Property(AdvanceCategory => AdvanceCategory.LastUpdated)
                .HasDefaultValueSql("(getutcdate())", "DF__AdvanceCa__LastU__73BA3083")
                .HasColumnType("datetime");
            builder.Property(AdvanceCategory => AdvanceCategory.Name).HasMaxLength(100);

            builder.HasOne(AdvanceCategory => AdvanceCategory.Organization).WithMany(Organization => Organization.AdvanceCategories)
                .HasForeignKey(AdvanceCategory => AdvanceCategory.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AdvanceCategories_Organizations");
        });

        modelBuilder.Entity<AuditTrail>(builder =>
        {
            builder.ToTable("AuditTrail");

            builder.Property(AuditTrail => AuditTrail.AccessTokenHash).HasMaxLength(500);
            builder.Property(AuditTrail => AuditTrail.ClientType).HasMaxLength(100);
            builder.Property(AuditTrail => AuditTrail.Description).HasMaxLength(255);
            builder.Property(AuditTrail => AuditTrail.DeviceIdentifier).HasMaxLength(255);
            builder.Property(AuditTrail => AuditTrail.EntryDate)
                .HasDefaultValueSql("(getutcdate())", "DF_AuditTrail_EntryDate")
                .HasColumnType("datetime");
            builder.Property(AuditTrail => AuditTrail.ExpiryDate).HasColumnType("datetime");
            builder.Property(AuditTrail => AuditTrail.IpAddress).HasMaxLength(50);
            builder.Property(AuditTrail => AuditTrail.RefreshTokenHash).HasMaxLength(500);
            builder.Property(AuditTrail => AuditTrail.RevokedDate).HasColumnType("datetime");
            builder.Property(AuditTrail => AuditTrail.UserAgent).HasMaxLength(500);

            builder.HasOne(AuditTrail => AuditTrail.Country).WithMany(Country => Country.AuditTrails)
                .HasForeignKey(AuditTrail => AuditTrail.CountryId)
                .HasConstraintName("FK_AuditTrail_Countries");

            builder.HasOne(AuditTrail => AuditTrail.User).WithMany(User => User.AuditTrails)
                .HasForeignKey(AuditTrail => AuditTrail.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_AuditTrail_Users");
        });

        modelBuilder.Entity<Country>(builder =>
        {
            builder.Property(Country => Country.Id).HasColumnName("ID");
            builder.Property(Country => Country.Code).HasMaxLength(255);
            builder.Property(Country => Country.CountryName)
                .HasMaxLength(255)
                .HasColumnName("CountryName");
        });

        modelBuilder.Entity<District>(builder =>
        {
            builder.Property(District => District.DistrictId).HasColumnName("DistrictID");
            builder.Property(District => District.DistrictName)
                .HasMaxLength(50)
                .HasColumnName("DistrictName");
            builder.Property(District => District.Position).HasDefaultValue(0, "DF_Districts_Position");
            builder.Property(District => District.State).HasMaxLength(255);
            builder.Property(District => District.TempDistrict).HasMaxLength(255);

            builder.HasOne(District => District.CountryNavigation).WithMany(Country => Country.Districts)
                .HasForeignKey(District => District.Country)
                .HasConstraintName("FK_Districts_Countries");

            builder.HasOne(District => District.RegionNavigation).WithMany(Region => Region.Districts)
                .HasForeignKey(District => District.Region)
                .HasConstraintName("FK_Districts_Regions");
        });

        modelBuilder.Entity<Organization>(builder =>
        {
            builder.HasKey(Organization => Organization.OrganizationId);
            builder.Property(Organization => Organization.Address).HasMaxLength(500);
            builder.Property(Organization => Organization.CreatedAt)
                .HasDefaultValueSql("(getutcdate())", "DF__Organizat__Creat__5629CD9C")
                .HasColumnType("datetime");
            builder.Property(Organization => Organization.Email).HasMaxLength(255);
            builder.Property(Organization => Organization.Logo).HasMaxLength(1000);
            builder.Property(Organization => Organization.OrganizationName).HasMaxLength(255);
            builder.Property(Organization => Organization.Telephone).HasMaxLength(50);
            builder.Property(Organization => Organization.UpdatedAt)
                .HasDefaultValueSql("(getutcdate())", "DF__Organizat__Updat__571DF1D5")
                .HasColumnType("datetime");

            builder.HasOne(Organization => Organization.Country).WithMany(Country => Country.Organizations)
                .HasForeignKey(Organization => Organization.CountryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Organizations_Countries");

            builder.HasOne(Organization => Organization.District).WithMany(District => District.Organizations)
                .HasForeignKey(Organization => Organization.DistrictId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Organizations_Districts");

            builder.HasOne(Organization => Organization.Region).WithMany(Region => Region.Organizations)
                .HasForeignKey(Organization => Organization.RegionId)
                .HasConstraintName("FK_Organizations_Regions");
        });

        modelBuilder.Entity<Permission>(builder =>
        {
            builder.Property(Permission => Permission.Description).HasMaxLength(500);
            builder.Property(Permission => Permission.PermissionName).HasMaxLength(100);
        });

        modelBuilder.Entity<Receipt>(builder =>
        {
            builder.Property(Receipt => Receipt.AmountInWords).HasMaxLength(200);
            builder.Property(Receipt => Receipt.ContentType).HasMaxLength(100);
            builder.Property(Receipt => Receipt.EntryDate)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            builder.Property(Receipt => Receipt.FileName).HasMaxLength(255);
            builder.Property(Receipt => Receipt.ReceiptNumber).HasMaxLength(100);

            builder.HasOne(Receipt => Receipt.Transaction).WithMany(Transaction => Transaction.Receipts)
                .HasForeignKey(Receipt => Receipt.TransactionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Receipts_Transactions");

            builder.HasOne(Receipt => Receipt.User).WithMany(User => User.Receipts)
                .HasForeignKey(Receipt => Receipt.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Receipts_Users");
        });

        modelBuilder.Entity<Region>(builder =>
        {
            builder.Property(Region => Region.RegionId).HasColumnName("RegionID");
            builder.Property(Region => Region.RegionName).HasMaxLength(50);
            builder.Property(Region => Region.State).HasMaxLength(255);

            builder.HasOne(Region => Region.CountryNavigation).WithMany(Country => Country.Regions)
                .HasForeignKey(Region => Region.Country)
                .HasConstraintName("FK_Regions_Countries");
        });

        modelBuilder.Entity<Role>(builder =>
        {
            builder.Property(Role => Role.Description).HasMaxLength(500);
            builder.Property(Role => Role.EntryDate)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            builder.Property(Role => Role.RoleName).HasMaxLength(100);

            builder.HasOne(Role => Role.Organization).WithMany(Organization => Organization.Roles)
                .HasForeignKey(Role => Role.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Roles_Organizations");
        });

        modelBuilder.Entity<Transaction>(builder =>
        {
            builder.HasIndex(Transaction => Transaction.ReferenceNumber, "UQ_Transactions_ReferenceNumber").IsUnique();

            builder.Property(Transaction => Transaction.Amount).HasColumnType("decimal(18, 2)");
            builder.Property(Transaction => Transaction.ApprovedDate).HasColumnType("datetime");
            builder.Property(Transaction => Transaction.Description).HasMaxLength(2000);
            builder.Property(Transaction => Transaction.EntryDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            builder.Property(Transaction => Transaction.LastUpdatedDate)
                .HasDefaultValueSql("(sysutcdatetime())")
                .HasColumnType("datetime");
            builder.Property(Transaction => Transaction.Notes).HasMaxLength(2000);
            builder.Property(Transaction => Transaction.ReconciliationDate).HasColumnType("datetime");
            builder.Property(Transaction => Transaction.RejectionReason).HasMaxLength(255);
            builder.Property(Transaction => Transaction.Status).HasMaxLength(50);
            builder.Property(Transaction => Transaction.TransactionDate).HasColumnType("datetime");

            builder.HasOne(Transaction => Transaction.ApprovedByUser).WithMany(User => User.TransactionApprovedByUsers)
                .HasForeignKey(Transaction => Transaction.ApprovedByUserId)
                .HasConstraintName("FK_Transactions_ApprovedByUser");

            builder.HasOne(Transaction => Transaction.Category).WithMany(Category => Category.Transactions)
                .HasForeignKey(Transaction => Transaction.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Transactions_AdvanceCategories");

            builder.HasOne(Transaction => Transaction.IssuedByUser).WithMany(User => User.TransactionIssuedByUsers)
                .HasForeignKey(Transaction => Transaction.IssuedByUserId)
                .HasConstraintName("FK_Transactions_IssuedByUser");

            builder.HasOne(Transaction => Transaction.Organization).WithMany(Organization => Organization.Transactions)
                .HasForeignKey(Transaction => Transaction.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Transactions_Organizations");

            builder.HasOne(Transaction => Transaction.ParentTransaction).WithMany(Transaction => Transaction.InverseParentTransaction)
                .HasForeignKey(Transaction => Transaction.ParentTransactionId)
                .HasConstraintName("FK_Transactions_ParentTransaction");

            builder.HasOne(Transaction => Transaction.ReconciledByUser).WithMany(User => User.TransactionReconciledByUsers)
                .HasForeignKey(Transaction => Transaction.ReconciledByUserId)
                .HasConstraintName("FK_Transactions_ReconciledByUser");

            builder.HasOne(Transaction => Transaction.TransactionType).WithMany(TransactionType => TransactionType.Transactions)
                .HasForeignKey(Transaction => Transaction.TransactionTypeId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Transactions_TransactionTypes");

            builder.HasOne(Transaction => Transaction.User).WithMany(User => User.TransactionUsers)
                .HasForeignKey(Transaction => Transaction.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Transactions_Users");
        });

        modelBuilder.Entity<TransactionType>(builder =>
        {
            builder.Property(TransactionType => TransactionType.Description).HasMaxLength(500);
            builder.Property(TransactionType => TransactionType.EntryDate)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");
            builder.Property(TransactionType => TransactionType.TransactionTypeName).HasMaxLength(100);
        });

        modelBuilder.Entity<User>(builder =>
        {
            builder.Property(User => User.Email).HasMaxLength(255);
            builder.Property(User => User.EntryDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            builder.Property(User => User.FirstName).HasMaxLength(50);
            builder.Property(User => User.Image).HasMaxLength(1000);
            builder.Property(User => User.IsActive).HasDefaultValue(true);
            builder.Property(User => User.LastLoginDate).HasColumnType("datetime");
            builder.Property(User => User.LastName).HasMaxLength(50);
            builder.Property(User => User.PasswordHash).HasMaxLength(255);
            builder.Property(User => User.TelephoneNumber).HasMaxLength(50);

            builder.HasOne(User => User.Country).WithMany(Country => Country.Users)
                .HasForeignKey(User => User.CountryId)
                .HasConstraintName("FK_Users_Countries");

            builder.HasOne(User => User.District).WithMany(District => District.Users)
                .HasForeignKey(User => User.DistrictId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Districts");

            builder.HasOne(User => User.Organization).WithMany(Organization => Organization.Users)
                .HasForeignKey(User => User.OrganizationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Users_Organizations");

            builder.HasOne(User => User.Region).WithMany(User => User.Users)
                .HasForeignKey(User => User.RegionId)
                .HasConstraintName("FK_Users_Regions");

            builder.HasOne(User => User.Role).WithMany(User => User.Users)
                .HasForeignKey(User => User.RoleId)
                .HasConstraintName("FK_Users_Roles");
        });

        modelBuilder.Entity<UserPermission>(builder =>
        {
            builder.Property(UserPermission => UserPermission.EntryDate)
                .HasDefaultValueSql("(getutcdate())")
                .HasColumnType("datetime");

            builder.HasOne(UserPermission => UserPermission.Permission).WithMany(Permission => Permission.UserPermissions)
                .HasForeignKey(UserPermission => UserPermission.PermissionId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserPermissions_Permissions");

            builder.HasOne(UserPermission => UserPermission.User).WithMany(User => User.UserPermissions)
                .HasForeignKey(UserPermission => UserPermission.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserPermissions_Users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
