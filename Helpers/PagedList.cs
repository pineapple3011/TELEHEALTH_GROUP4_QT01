using Microsoft.EntityFrameworkCore;

namespace TELEHEALTH_GROUP4_QT01.Helpers;

/// <summary>
/// Server-side paging helper. Counting and Skip/Take are translated to SQL (OFFSET/FETCH),
/// so only one page of rows is read from the database.
/// The query passed in MUST already have an OrderBy (required for stable paging).
/// </summary>
public class PagedList<T>
{
    public IReadOnlyList<T> Items { get; }
    public int Page { get; }
    public int PageSize { get; }
    public int TotalItems { get; }
    public int TotalPages => (int)Math.Ceiling(TotalItems / (double)PageSize);

    private PagedList(IReadOnlyList<T> items, int page, int pageSize, int totalItems)
    {
        Items = items;
        Page = page;
        PageSize = pageSize;
        TotalItems = totalItems;
    }

    public static async Task<PagedList<T>> CreateAsync(
        IQueryable<T> source, int page, int pageSize, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await source.CountAsync(ct);
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Clamp(page, 1, pages);          // page out of range -> nearest valid page

        var items = await source.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedList<T>(items, page, pageSize, total);
    }
}
