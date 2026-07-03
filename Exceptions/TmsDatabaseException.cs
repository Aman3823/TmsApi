namespace TmsApi.Data;

public class TmsDatabaseException : Exception
{
    public TmsDatabaseException(string message) : base(message)
    {
    }
}