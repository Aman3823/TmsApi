namespace TmsApi.Api.Exceptions;

public class TmsDatabaseException : Exception
{
    public TmsDatabaseException(string message) : base(message)
    {
    }
}