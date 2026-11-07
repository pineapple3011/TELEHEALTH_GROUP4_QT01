using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TELEHEALTH_GROUP4_QT01.Models;
using TELEHEALTH_GROUP4_QT01.Models.Dtos;

namespace TELEHEALTH_GROUP4_QT01.ApiControllers;

/// <summary>Medical specialties (read-only, public).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/specialties")]
[Produces("application/json")]
public class SpecialtiesApiController : ControllerBase
{
    private readonly TelehealthContext _db;
    public SpecialtiesApiController(TelehealthContext db) => _db = db;

    /// <summary>List all specialties with base price and number of doctors.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<SpecialtyDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<SpecialtyDto>>> GetAll()
    {
        var list = await _db.Specialties.AsNoTracking()
            .OrderBy(s => s.SpecialtyName)
            .Select(s => new SpecialtyDto(s.SpecId, s.SpecialtyName, s.Description, s.BasePrice, s.Doctors.Count))
            .ToListAsync();
        return Ok(list);
    }
}
