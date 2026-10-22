using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class Clinic
{
    public int ClinicId { get; set; }

    public int SpecId { get; set; }

    public string ClinicName { get; set; } = null!;

    public string Location { get; set; } = null!;

    public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();

    public virtual Specialty Spec { get; set; } = null!;
}
