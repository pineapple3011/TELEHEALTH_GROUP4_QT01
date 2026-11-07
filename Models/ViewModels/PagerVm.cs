namespace TELEHEALTH_GROUP4_QT01.Models.ViewModels;

/// <summary>Input for the shared _Pager partial view.</summary>
public class PagerVm
{
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;

    /// <summary>Builds the URL of page p (keeps the current filters). Set it with Url.Action(...).</summary>
    public Func<int, string?> UrlFor { get; set; } = _ => "#";
}
