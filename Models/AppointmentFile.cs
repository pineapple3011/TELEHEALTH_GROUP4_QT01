using System;
using System.Collections.Generic;

namespace TELEHEALTH_GROUP4_QT01.Models;

public partial class AppointmentFile
{
    public int FileId { get; set; }

    public int AppointId { get; set; }

    public string FilePath { get; set; } = null!;

    public string FileType { get; set; } = null!;

    public DateTime UploadDate { get; set; }

    public virtual Appointment Appoint { get; set; } = null!;
}
