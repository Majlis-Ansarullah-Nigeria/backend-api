namespace ManagementApi.Application.Common.Models;

public class PaginationFilter
{
    public string? SearchString { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    public PaginationFilter()
    {
    }

    public PaginationFilter(int pageNumber, int pageSize, string? searchString = null)
    {
        PageNumber = pageNumber < 1 ? 1 : pageNumber;
        PageSize = pageSize > 100 ? 100 : pageSize; // Max 100 items per page
        SearchString = searchString;
    }
}
