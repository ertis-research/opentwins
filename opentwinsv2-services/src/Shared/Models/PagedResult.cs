namespace OpenTwinsV2.Shared.Models
{
    public record PagedResult<T>(
        IEnumerable<T> Items, 
        int TotalCount, 
        int CurrentPage, 
        int PageSize, 
        int TotalPages
    );
}