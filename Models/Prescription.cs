using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class Prescription
{
    public int RecordId { get; set; }

    public int MedicineId { get; set; }

    public int Quantity { get; set; }

    public string Dosage { get; set; } = null!;

    public string? Hds { get; set; }

    public virtual Medicine Medicine { get; set; } = null!;

    public virtual MedicalRecord Record { get; set; } = null!;
}
