using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class Doctor
{
    public int DoctorId { get; set; }

    public int SpecId { get; set; }

    public int? AdminId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public int Exp { get; set; }

    public string Current { get; set; } = null!;

    public virtual Admin? Admin { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual ICollection<Schedule> Schedules { get; set; } = new List<Schedule>();

    public virtual Specialty Spec { get; set; } = null!;
}
