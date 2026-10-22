using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class Appointment
{
    public int AppointId { get; set; }

    public int PatientId { get; set; }

    public int DoctorId { get; set; }

    public int? ClinicId { get; set; }

    public int? ScheduleId { get; set; }

    public DateOnly AppointmentDate { get; set; }

    public TimeOnly AppointmentTime { get; set; }

    public string BookingType { get; set; } = null!;

    public string? EmergencyLocation { get; set; }

    public string? TriageLevel { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<AppointmentFile> AppointmentFiles { get; set; } = new List<AppointmentFile>();

    public virtual Clinic? Clinic { get; set; }

    public virtual Doctor Doctor { get; set; } = null!;

    public virtual MedicalRecord? MedicalRecord { get; set; }

    public virtual Patient Patient { get; set; } = null!;

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual Schedule? Schedule { get; set; }
}
