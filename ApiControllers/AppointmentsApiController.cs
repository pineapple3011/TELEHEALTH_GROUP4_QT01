using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.Dtos;
using TELEHEALTH_GROUP4_QT01.Security;

namespace TELEHEALTH_GROUP4_QT01.ApiControllers;

/// <summary>
/// Appointments of the signed-in PATIENT. Role is checked on the server ([Authorize]) and the
/// patient id comes from the server-issued claim, never from the request body/query.
/// </summary>
[ApiController]
[Authorize(Roles = AppRoles.Patient)]
[Route("api/appointments")]
[Produces("application/json")]
public class AppointmentsApiController : ControllerBase
{
    private readonly TelehealthContext _db;
    private readonly ILogger<AppointmentsApiController> _logger;

    public AppointmentsApiController(TelehealthContext db, ILogger<AppointmentsApiController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private int? CurrentPatientId =>
        int.TryParse(User.FindFirstValue(AppClaims.PatientId), out var id) && id > 0 ? id : null;

    /// <summary>List my appointments (paged, optional status filter).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AppointmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AppointmentDto>>> GetMine(string? status, int page = 1, int pageSize = 10)
    {
        if (CurrentPatientId is not int pid) return Forbid();

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Appointments.AsNoTracking().Where(a => a.PatientId == pid);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.AppointmentTime)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AppointmentDto(a.AppointId, a.DoctorId, a.Doctor.FullName, a.AppointmentDate,
                                            a.AppointmentTime, a.BookingType, a.TriageLevel, a.Status))
            .ToListAsync();

        return Ok(new PagedResult<AppointmentDto>(items, page, pageSize, total));
    }

    /// <summary>Get one of my appointments. Other people's ids return 404 (no information leak).</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AppointmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AppointmentDto>> GetOne(int id)
    {
        if (CurrentPatientId is not int pid) return Forbid();

        var dto = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointId == id && a.PatientId == pid)
            .Select(a => new AppointmentDto(a.AppointId, a.DoctorId, a.Doctor.FullName, a.AppointmentDate,
                                            a.AppointmentTime, a.BookingType, a.TriageLevel, a.Status))
            .FirstOrDefaultAsync();
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Cancel one of my appointments (soft delete: status becomes "cancelled").</summary>
    /// <remarks>
    /// [IgnoreAntiforgeryToken]: this JSON endpoint cannot carry a form token. CSRF is mitigated by
    /// the auth cookie (SameSite=Lax, HttpOnly, Secure) and because DELETE is a non-simple
    /// cross-origin request (CORS is not enabled).
    /// </remarks>
    [HttpDelete("{id:int}")]
    [IgnoreAntiforgeryToken]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(int id)
    {
        if (CurrentPatientId is not int pid) return Forbid();

        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.AppointId == id && a.PatientId == pid);
        if (appt is null) return NotFound();

        if (appt.Status != TrangThaiCa.ChoXacNhan && appt.Status != TrangThaiCa.DaXacNhan)
            return Conflict(new ProblemDetails
            {
                Title = "Appointment cannot be cancelled",
                Detail = $"Current status: {appt.Status}",
                Status = StatusCodes.Status409Conflict
            });

        appt.Status = TrangThaiCa.DaHuy;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Patient {PatientId} cancelled appointment {AppointId} via API", pid, id);
        return NoContent();
    }
}
