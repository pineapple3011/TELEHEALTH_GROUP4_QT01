namespace TELEHEALTH_GROUP4_QT01.Models.ViewModels
{
    public class AboutVm
    {
        public int TotalDoctors { get; set; }
        public int TotalPatients { get; set; }
        public int TotalAppointments { get; set; }
        public List<AboutBarVm> Bars { get; set; } = new();
        public List<AboutServiceVm> Services { get; set; } = new();
    }

    public class AboutBarVm
    {
        public string Label { get; set; } = "";
        public int Count { get; set; }
        public int Percent { get; set; }
    }

    public class AboutServiceVm
    {
        public string Name { get; set; } = "";
        public int DoctorCount { get; set; }
        public int AppointmentCount { get; set; }
    }
}