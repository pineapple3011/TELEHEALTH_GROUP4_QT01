namespace TELEHEALTH_GROUP4_QT01.Models.Dtos;

// DTOs returned by the REST API. Entities are never exposed directly
// (avoids leaking fields such as e-mail/phone and circular references).

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems)
{
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);
}

public record DoctorDto(
    int DoctorId,
    string FullName,
    int SpecialtyId,
    string SpecialtyName,
    int ExperienceYears,
    string Status,
    decimal BaseFee,
    string? AvatarUrl);

public record SpecialtyDto(
    int SpecialtyId,
    string Name,
    string? Description,
    decimal BasePrice,
    int DoctorCount);

public record SlotDto(int ScheduleId, string Time);

public record AppointmentDto(
    int AppointmentId,
    int DoctorId,
    string DoctorName,
    DateOnly Date,
    TimeOnly Time,
    string BookingType,
    string? TriageLevel,
    string Status);
