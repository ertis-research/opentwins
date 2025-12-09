namespace OpenTwinsV2.Things.Models
{
    public record PagedResult<T>(
        IEnumerable<T> Items, 
        int TotalCount, 
        int CurrentPage, 
        int PageSize, 
        int TotalPages
    );
}