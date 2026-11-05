using Microsoft.EntityFrameworkCore;

namespace TELEHEALTH_GROUP4_QT01.Models;

// File riêng (partial) để khi scaffold lại DB, phần đăng nhập không bị ghi đè.
public partial class TelehealthContext
{
    public virtual DbSet<UserAccount> UserAccounts { get; set; }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("UserAccounts");
            entity.HasKey(e => e.UserId);

            entity.Property(e => e.UserId).HasColumnName("UserID");
            entity.Property(e => e.Email).HasMaxLength(100).IsUnicode(false);
            entity.HasIndex(e => e.Email, "UQ_UserAccounts_Email").IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(256).IsUnicode(false);
            entity.Property(e => e.Role).HasMaxLength(20).IsUnicode(false);
            entity.Property(e => e.PatientId).HasColumnName("PatientID");
            entity.Property(e => e.DoctorId).HasColumnName("DoctorID");
            entity.Property(e => e.AdminId).HasColumnName("AdminID");
            entity.Property(e => e.SecurityStamp).HasMaxLength(64).IsUnicode(false);
            entity.Property(e => e.CreatedAtUtc).HasColumnType("datetime2");
            entity.Property(e => e.LockoutEndUtc).HasColumnType("datetime2");
            entity.Property(e => e.LastLoginUtc).HasColumnType("datetime2");

            entity.HasOne(d => d.Patient).WithMany()
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAccounts_Patients");

            entity.HasOne(d => d.Doctor).WithMany()
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAccounts_Doctors");

            entity.HasOne(d => d.Admin).WithMany()
                .HasForeignKey(d => d.AdminId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_UserAccounts_Admin");
        });
    }
}
