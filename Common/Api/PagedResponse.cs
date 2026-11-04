namespace Crm.Api.Common.Api;

public class PagedResponse<T>
{
    public IEnumerable<T> Items { get; set; } = [];

    public int CurrentPage { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public int TotalPages { get; set; }

    public bool HasPreviousPage => CurrentPage > 1;

    public bool HasNextPage => CurrentPage < TotalPages;
}