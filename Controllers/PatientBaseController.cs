using Microsoft.AspNetCore.Mvc;

namespace TELEHEALTH_GROUP4_QT01.Controllers
{
    public abstract class PatientBaseController : Controller
    {
        // TẠM THỜI: cố định bệnh nhân số 1 (phải có dòng PatientID = 1 trong bảng BENH_NHAN).
        // Khi nhóm làm xong đăng nhập, chỉ cần sửa đúng dòng này (lấy ID từ Session/Claims).
        protected int CurrentPatientId => 1;
    }
}
