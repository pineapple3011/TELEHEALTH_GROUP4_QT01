using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class MedicalRecord
{
    public int RecordId { get; set; }

    public int AppointId { get; set; }

    public string Diagnosis { get; set; } = null!;

    public DateOnly? FollowUpDate { get; set; }

    public DateTime CreateAt { get; set; }

    public virtual Appointment Appoint { get; set; } = null!;

    public virtual ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
}
