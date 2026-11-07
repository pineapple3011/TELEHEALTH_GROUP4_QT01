using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Helpers;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.Dtos;

namespace TELEHEALTH_GROUP4_QT01.ApiControllers;

/// <summary>Public doctor directory (read-only).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/doctors")]
[Produces("application/json")]
public class DoctorsApiController : ControllerBase
{
    private readonly TelehealthContext _db;
    public DoctorsApiController(TelehealthContext db) => _db = db;

    /// <summary>List doctors with filtering and server-side paging.</summary>
    /// <param name="q">Part of the doctor's name.</param>
    /// <param name="specId">Filter by specialty id.</param>
    /// <param name="onlyAvailable">Only doctors whose status is Available.</param>
    /// <param name="page">Page number (starts at 1).</param>
    /// <param name="pageSize">Items per page (1-50).</param>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DoctorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DoctorDto>>> GetDoctors(
        string? q, int? specId, bool onlyAvailable = false, int page = 1, int pageSize = 10)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.Doctors.AsNoTracking().AsQueryable();
        if (specId.HasValue) query = query.Where(d => d.SpecId == specId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var keyword = q.Trim();
            query = query.Where(d => d.FullName.Contains(keyword));
        }
        if (onlyAvailable) query = query.Where(d => d.Current == "Available");

        var total = await query.CountAsync();
        var items = await query
            .OrderBy(d => d.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DoctorDto(d.DoctorId, d.FullName, d.SpecId, d.Spec.SpecialtyName,
                                       d.Exp, d.Current, d.Spec.BasePrice, d.AvatarUrl))
            .ToListAsync();

        return Ok(new PagedResult<DoctorDto>(items, page, pageSize, total));
    }

    /// <summary>Get one doctor by id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(DoctorDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DoctorDto>> GetDoctor(int id)
    {
        var dto = await _db.Doctors.AsNoTracking()
            .Where(d => d.DoctorId == id)
            .Select(d => new DoctorDto(d.DoctorId, d.FullName, d.SpecId, d.Spec.SpecialtyName,
                                       d.Exp, d.Current, d.Spec.BasePrice, d.AvatarUrl))
            .FirstOrDefaultAsync();
        return dto is null ? NotFound() : Ok(dto);
    }

    /// <summary>Free 30-minute slots of a doctor on a given date (yyyy-MM-dd).</summary>
    [HttpGet("{id:int}/slots")]
    [ProducesResponseType(typeof(IEnumerable<SlotDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<SlotDto>>> GetFreeSlots(int id, [FromQuery] DateOnly date)
    {
        if (!await _db.Doctors.AnyAsync(d => d.DoctorId == id)) return NotFound();

        var schedules = await _db.Schedules.AsNoTracking()
            .Where(s => s.DoctorId == id && s.WorkDate == date)
            .ToListAsync();
        var scheduleIds = schedules.Select(s => s.ScheduleId).ToList();

        var booked = await _db.Appointments.AsNoTracking()
            .Where(a => a.ScheduleId != null && scheduleIds.Contains(a.ScheduleId.Value))
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = TimeOnly.FromDateTime(DateTime.Now);

        var result = new List<SlotDto>();
        foreach (var s in schedules)
        {
            var free = SlotHelper.FreeSlots(s, booked.Where(a => a.ScheduleId == s.ScheduleId));
            if (date == today) free = free.Where(t => t > now).ToList();   // drop past slots
            result.AddRange(free.Select(t => new SlotDto(s.ScheduleId, t.ToString("HH:mm"))));
        }
        return Ok(result.OrderBy(x => x.Time));
    }
}
