using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class TelehealthContext : DbContext
{
    public TelehealthContext()
    {
    }

    public TelehealthContext(DbContextOptions<TelehealthContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Admin> Admins { get; set; }

    public virtual DbSet<Appointment> Appointments { get; set; }

    public virtual DbSet<AppointmentFile> AppointmentFiles { get; set; }

    public virtual DbSet<Clinic> Clinics { get; set; }

    public virtual DbSet<Doctor> Doctors { get; set; }

    public virtual DbSet<MedicalRecord> MedicalRecords { get; set; }

    public virtual DbSet<Medicine> Medicines { get; set; }

    public virtual DbSet<Patient> Patients { get; set; }

    public virtual DbSet<Payment> Payments { get; set; }

    public virtual DbSet<Prescription> Prescriptions { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<Specialty> Specialties { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Data Source=.;Initial Catalog=TELEHEALTH;Integrated Security=True;Connect Timeout=30;Encrypt=True;Trust Server Certificate=True;Application Intent=ReadWrite;Multi Subnet Failover=False;Command Timeout=30");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Admin>(entity =>
        {
            entity.ToTable("ADMIN");

            entity.HasIndex(e => e.Email, "UQ_ADMIN_Email").IsUnique();

            entity.Property(e => e.AdminId).HasColumnName("AdminID");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.AppointId);

            entity.Property(e => e.AppointId).HasColumnName("AppointID");
            entity.Property(e => e.BookingType).HasMaxLength(50);
            entity.Property(e => e.ClinicId).HasColumnName("ClinicID");
            entity.Property(e => e.DoctorId).HasColumnName("DoctorID");
            entity.Property(e => e.EmergencyLocation).HasMaxLength(255);
            entity.Property(e => e.PatientId).HasColumnName("PatientID");
            entity.Property(e => e.ScheduleId).HasColumnName("ScheduleID");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.TriageLevel).HasMaxLength(50);

            entity.HasOne(d => d.Clinic).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.ClinicId)
                .HasConstraintName("FK_Appointments_Clinics");

            entity.HasOne(d => d.Doctor).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Doctors");

            entity.HasOne(d => d.Patient).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.PatientId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Appointments_Patients");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Appointments)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("FK_Appointments_Schedules");
        });

        modelBuilder.Entity<AppointmentFile>(entity =>
        {
            entity.HasKey(e => e.FileId);

            entity.Property(e => e.FileId).HasColumnName("FileID");
            entity.Property(e => e.AppointId).HasColumnName("AppointID");
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.FileType)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.UploadDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Appoint).WithMany(p => p.AppointmentFiles)
                .HasForeignKey(d => d.AppointId)
                .HasConstraintName("FK_AppointmentFiles_Appointments");
        });

        modelBuilder.Entity<Clinic>(entity =>
        {
            entity.Property(e => e.ClinicId).HasColumnName("ClinicID");
            entity.Property(e => e.ClinicName).HasMaxLength(100);
            entity.Property(e => e.Location).HasMaxLength(255);
            entity.Property(e => e.SpecId).HasColumnName("SpecID");

            entity.HasOne(d => d.Spec).WithMany(p => p.Clinics)
                .HasForeignKey(d => d.SpecId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Clinics_Specialties");
        });

        modelBuilder.Entity<Doctor>(entity =>
        {
            entity.HasIndex(e => e.Email, "UQ_Doctors_Email").IsUnique();

            entity.Property(e => e.DoctorId).HasColumnName("DoctorID");
            entity.Property(e => e.AdminId).HasColumnName("AdminID");
            entity.Property(e => e.Current)
                .HasMaxLength(50)
                .HasDefaultValue("Available");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.Exp).HasColumnName("EXP");
            entity.Property(e => e.AvatarUrl).HasMaxLength(300);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.SpecId).HasColumnName("SpecID");

            entity.HasOne(d => d.Admin).WithMany(p => p.Doctors)
                .HasForeignKey(d => d.AdminId)
                .HasConstraintName("FK_Doctors_ADMIN");

            entity.HasOne(d => d.Spec).WithMany(p => p.Doctors)
                .HasForeignKey(d => d.SpecId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Doctors_Specialties");
        });

        modelBuilder.Entity<MedicalRecord>(entity =>
        {
            entity.HasKey(e => e.RecordId);

            entity.HasIndex(e => e.AppointId, "UQ_MedicalRecords_AppointID").IsUnique();

            entity.Property(e => e.RecordId).HasColumnName("RecordID");
            entity.Property(e => e.AppointId).HasColumnName("AppointID");
            entity.Property(e => e.CreateAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.Appoint).WithOne(p => p.MedicalRecord)
                .HasForeignKey<MedicalRecord>(d => d.AppointId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_MedicalRecords_Appointments");
        });

        modelBuilder.Entity<Medicine>(entity =>
        {
            entity.Property(e => e.MedicineId).HasColumnName("MedicineID");
            entity.Property(e => e.Hds)
                .HasMaxLength(255)
                .HasColumnName("HDS");
            entity.Property(e => e.Ingredient).HasMaxLength(500);
            entity.Property(e => e.MedicineName).HasMaxLength(150);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
        });

        modelBuilder.Entity<Patient>(entity =>
        {
            entity.HasIndex(e => e.Email, "UQ_Patients_Email").IsUnique();

            entity.Property(e => e.PatientId).HasColumnName("PatientID");
            entity.Property(e => e.Address).HasMaxLength(255);
            entity.Property(e => e.AdminId).HasColumnName("Admin_ID");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.EmergencyPhone)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .IsUnicode(false);

            entity.HasOne(d => d.Admin).WithMany(p => p.Patients)
                .HasForeignKey(d => d.AdminId)
                .HasConstraintName("FK_Patients_ADMIN");
        });

        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payment");

            entity.Property(e => e.PaymentId).HasColumnName("PaymentID");
            entity.Property(e => e.Amount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.AppointId).HasColumnName("AppointID");
            entity.Property(e => e.PaymentMethod).HasMaxLength(50);
            entity.Property(e => e.PaymentTime)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");

            entity.HasOne(d => d.Appoint).WithMany(p => p.Payments)
                .HasForeignKey(d => d.AppointId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Payment_Appointments");
        });

        modelBuilder.Entity<Prescription>(entity =>
        {
            entity.HasKey(e => new { e.RecordId, e.MedicineId });

            entity.ToTable("Prescription");

            entity.Property(e => e.RecordId).HasColumnName("RecordID");
            entity.Property(e => e.MedicineId).HasColumnName("MedicineID");
            entity.Property(e => e.Dosage).HasMaxLength(100);
            entity.Property(e => e.Hds)
                .HasMaxLength(255)
                .HasColumnName("HDS");
            entity.Property(e => e.Quantity).HasDefaultValue(1);

            entity.HasOne(d => d.Medicine).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.MedicineId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Prescription_Medicines");

            entity.HasOne(d => d.Record).WithMany(p => p.Prescriptions)
                .HasForeignKey(d => d.RecordId)
                .HasConstraintName("FK_Prescription_MedicalRecords");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.Property(e => e.ScheduleId).HasColumnName("ScheduleID");
            entity.Property(e => e.DoctorId).HasColumnName("DoctorID");

            entity.HasOne(d => d.Doctor).WithMany(p => p.Schedules)
                .HasForeignKey(d => d.DoctorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Schedules_Doctors");
        });

        modelBuilder.Entity<Specialty>(entity =>
        {
            entity.HasKey(e => e.SpecId);

            entity.Property(e => e.SpecId).HasColumnName("SpecID");
            entity.Property(e => e.BasePrice).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.SpecialtyName).HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
