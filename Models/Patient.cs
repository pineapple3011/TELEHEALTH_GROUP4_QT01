using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class Patient
{
    public int PatientId { get; set; }

    public int? AdminId { get; set; }

    public string FullName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Address { get; set; }

    public string? EmergencyPhone { get; set; }

    public virtual Admin? Admin { get; set; }

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
}
