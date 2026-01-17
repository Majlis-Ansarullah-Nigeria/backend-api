namespace ManagementApi.Application.Common.Models;

public class PaginationResponse<T>
{
    public List<T> Data { get; set; }
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;

    public PaginationResponse(List<T> data, int totalCount, int pageNumber, int pageSize)
    {
        Data = data;
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
    }

    public PaginationResponse()
    {
        Data = new List<T>();
    }

    public static PaginationResponse<T> Success(List<T> data, int totalCount, int pageNumber, int pageSize)
    {
        return new PaginationResponse<T>(data, totalCount, pageNumber, pageSize);
    }
}
