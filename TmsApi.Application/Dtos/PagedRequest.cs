namespace TmsApi.Application.Dtos;
public record PagedRequest
{
    private const int MaxPageSize = 50;
    private int _PageSize =20;
    public int Page {get ; init;} = 1;
    public int PageSize
    {
        get => _PageSize;
        init =>_PageSize = value < 1 ? 20 : value > MaxPageSize ? MaxPageSize : value;
    }
    public string? Search {get; init;}
    public string OrderBY {get; init;} ="Title";
    public bool Descending {get; init;}
}