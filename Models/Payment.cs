using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class Payment
{
    public int PaymentId { get; set; }

    public int AppointId { get; set; }

    public decimal Amount { get; set; }

    public string PaymentMethod { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime PaymentTime { get; set; }

    public virtual Appointment Appoint { get; set; } = null!;
}
