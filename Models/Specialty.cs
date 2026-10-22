using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class Specialty
{
    public int SpecId { get; set; }

    public string SpecialtyName { get; set; } = null!;

    public string? Description { get; set; }

    public decimal BasePrice { get; set; }

    public virtual ICollection<Clinic> Clinics { get; set; } = new List<Clinic>();

    public virtual ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();
}
